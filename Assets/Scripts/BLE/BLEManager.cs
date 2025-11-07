using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using YIUIFramework;

namespace ZZBJ
{
    // 蓝牙设备连接信息
    [System.Serializable]
    public class BLEDeviceInfo
    {
        public string DeviceName;
        public string DeviceAddress;
        public string ServiceNotifyUUID = "fff0";
        public string ServiceWriterUUID = "fff0";
        public string CharacteristicNotifyUUID = "fff1";
        public string CharacteristicWriterUUID = "fff2";
        
        // 设备连接状态
        [ShowInInspector] public bool IsConnected { get; set; }
        [ShowInInspector] public bool IsConnecting { get; set; }
        [ShowInInspector] public bool HasFoundService { get; set; }
        [ShowInInspector] public bool HasSubscribed { get; set; }
        
        // 数据缓存
        public byte[] CacheBytes;
        
        // 连接时间戳，用于控制连接顺序
        public float ConnectionStartTime;
        
        public BLEDeviceInfo(string deviceName)
        {
            DeviceName = deviceName;
        }
        
        public BLEDeviceInfo(string deviceName, string serviceNotifyUUID, string serviceWriterUUID, 
                           string characteristicNotifyUUID, string characteristicWriterUUID)
        {
            DeviceName = deviceName;
            ServiceNotifyUUID = serviceNotifyUUID;
            ServiceWriterUUID = serviceWriterUUID;
            CharacteristicNotifyUUID = characteristicNotifyUUID;
            CharacteristicWriterUUID = characteristicWriterUUID;
        }
    }

    // 蓝牙数据接收接口
    public interface IBLEDataHandler
    {
        void HandleData(string deviceName, byte[] data);
    }

    public sealed class BLEManager : MonoSingleton<BLEManager>
    {
        [ShowInInspector] public const string DefaultDeviceName = "TTHost2";

        /// <summary>
        /// 所有目标连接的设备
        /// </summary>
        [ShowInInspector] 
        public Dictionary<string, BLEDeviceInfo> TargetDevices = new Dictionary<string, BLEDeviceInfo>();

        /// <summary>
        /// 所有已连接的蓝牙设备
        /// </summary>
        [ShowInInspector]
        public Dictionary<string, BLEDeviceInfo> ConnectedDevices = new Dictionary<string, BLEDeviceInfo>();

        /// <summary>
        /// 正在连接的设备队列
        /// </summary>
        private Queue<string> _pendingConnectionQueue = new Queue<string>();

        /// <summary>
        /// 接收蓝牙数据时要处理的事件
        /// </summary>
        private List<IBLEDataHandler> dataHandlers = new List<IBLEDataHandler>();

        private bool _isScanning = false;
        private bool _isInitialized = false;
        private float _lastScanTime = 0f;
        private const float SCAN_RESTART_INTERVAL = 10f;

        // 注册数据处理器
        public void RegisterDataHandler(IBLEDataHandler handler)
        {
            if (!dataHandlers.Contains(handler))
            {
                dataHandlers.Add(handler);
            }
        }

        // 注销数据处理器
        public void UnregisterDataHandler(IBLEDataHandler handler)
        {
            if (dataHandlers.Contains(handler))
            {
                dataHandlers.Remove(handler);
            }
        }
        
        // 处理接收到的数据
        private void ProcessData(string deviceName, byte[] bytes)
        {
            // 调用所有能处理此设备类型的数据处理器
            foreach (var handler in dataHandlers)
            {
                handler.HandleData(deviceName, bytes);
            }
        }

        /// <summary>
        /// 添加要连接的设备
        /// </summary>
        public void AddTargetDevice(string deviceName, string serviceNotifyUUID = "fff0", string serviceWriterUUID = "fff0", 
            string characteristicNotifyUUID = "fff1", string characteristicWriterUUID = "fff2")
        {
            if (!TargetDevices.ContainsKey(deviceName))
            {
                var deviceInfo = new BLEDeviceInfo(deviceName, serviceNotifyUUID, serviceWriterUUID, characteristicNotifyUUID, characteristicWriterUUID);
                TargetDevices[deviceName] = deviceInfo;
                Debug.Log($"添加目标设备: {deviceName}");
            }
        }

        /// <summary>
        /// 移除目标设备
        /// </summary>
        public void RemoveTargetDevice(string deviceName)
        {
            if (TargetDevices.ContainsKey(deviceName))
            {
                if (ConnectedDevices.ContainsKey(deviceName))
                {
                    DisconnectDevice(deviceName).Forget();
                }
                TargetDevices.Remove(deviceName);
                Debug.Log($"移除目标设备: {deviceName}");
            }
        }

        /// <summary>
        /// 移除所有蓝牙设备
        /// </summary>
        public void RemoveAllTargetDevices()
        {
            TargetDevices.Clear();
        }

        /// <summary>
        /// 断开指定设备的连接
        /// </summary>
        public async UniTask DisconnectDevice(string deviceName)
        {
            if (ConnectedDevices.TryGetValue(deviceName, out var deviceInfo))
            {
                Debug.Log($"断开设备连接: {deviceName}");
                
                if (deviceInfo.HasSubscribed)
                {
                    BluetoothLEHardwareInterface.UnSubscribeCharacteristic(deviceInfo.DeviceAddress, 
                        deviceInfo.ServiceNotifyUUID, deviceInfo.CharacteristicNotifyUUID, null);
                }
                
                BluetoothLEHardwareInterface.DisconnectPeripheral(deviceInfo.DeviceAddress, (address) =>
                {
                    Debug.Log($"设备已断开: {deviceName}");
                    deviceInfo.IsConnected = false;
                    deviceInfo.IsConnecting = false;
                    deviceInfo.HasFoundService = false;
                    deviceInfo.HasSubscribed = false;
                    ConnectedDevices.Remove(deviceName);
                });
                
                await UniTask.Delay(1000);
            }
        }

        /// <summary>
        /// 断开所有设备连接
        /// </summary>
        public async UniTask DisconnectAllDevices()
        {
            var disconnectTasks = new List<UniTask>();
            foreach (var deviceName in ConnectedDevices.Keys.ToList())
            {
                disconnectTasks.Add(DisconnectDevice(deviceName));
            }
            
            await UniTask.WhenAll(disconnectTasks);
            await StopScan();
        }

        /// <summary>
        /// 停止扫描
        /// </summary>
        public async UniTask StopScan()
        {
            _isScanning = false;
            BluetoothLEHardwareInterface.StopScan();
            await UniTask.Delay(100);
        }

        /// <summary>
        /// 检查是否所有目标设备都已连接
        /// </summary>
        private bool AreAllTargetDevicesConnected()
        {
            foreach (var device in TargetDevices)
            {
                if (!device.Value.IsConnected)
                    return false;
            }
            return TargetDevices.Count > 0; // 确保至少有一个目标设备
        }

        /// <summary>
        /// 获取未连接的设备数量
        /// </summary>
        private int GetUnconnectedDeviceCount()
        {
            return TargetDevices.Count(device => !device.Value.IsConnected);
        }

        // 开始扫描数据
        public async UniTask StartScan()
        {
            if (_isScanning)
            {
                Debug.Log("已经在扫描中");
                return;
            }

            Debug.Log($"开始搜索蓝牙服务，目标设备数量: {TargetDevices.Count}");
            _isScanning = true;
            
            // 重置所有设备的连接状态
            foreach (var device in TargetDevices.Values)
            {
                device.IsConnected = false;
                device.IsConnecting = false;
                device.HasFoundService = false;
                device.HasSubscribed = false;
                device.DeviceAddress = null;
            }
            ConnectedDevices.Clear();
            _pendingConnectionQueue.Clear();

            if (!_isInitialized)
            {
                await InitializeBluetooth();
            }
            else
            {
                StartBluetoothScan();
            }
        }

        /// <summary>
        /// 初始化蓝牙
        /// </summary>
        private async UniTask InitializeBluetooth()
        {
            var initCompleted = false;
            
            BluetoothLEHardwareInterface.Initialize(true, false, () =>
            {
                Debug.Log("蓝牙初始化成功");
                _isInitialized = true;
                initCompleted = true;
                StartBluetoothScan();
            },
            (error) =>
            {
                Debug.LogError($"蓝牙初始化失败: {error}");
                _isScanning = false;
                initCompleted = true;
            });

            await UniTask.WaitUntil(() => initCompleted);
            await UniTask.Delay(500); // 给初始化一些额外时间
        }

        /// <summary>
        /// 开始蓝牙扫描
        /// </summary>
        private void StartBluetoothScan()
        {
            if (!_isScanning) return;

            Debug.Log("开始蓝牙设备扫描...");
            _lastScanTime = Time.time;
            
            // 开始扫描，不停止直到手动停止或所有设备连接
            BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, OnDeviceDiscovered, OnDeviceDiscoveredWithData, false);
            
            Debug.Log($"正在寻找以下设备: {string.Join(", ", TargetDevices.Keys)}");
        }

        /// <summary>
        /// 处理设备连接队列
        /// </summary>
        private void ProcessConnectionQueue()
        {
            if (_pendingConnectionQueue.Count > 0 && !IsAnyDeviceConnecting())
            {
                var nextDevice = _pendingConnectionQueue.Dequeue();
                if (TargetDevices.TryGetValue(nextDevice, out var deviceInfo) && 
                    !string.IsNullOrEmpty(deviceInfo.DeviceAddress) &&
                    !deviceInfo.IsConnected && !deviceInfo.IsConnecting)
                {
                    Debug.Log($"从队列中连接设备: {nextDevice}");
                    StartDeviceConnection(deviceInfo);
                }
            }
        }

        /// <summary>
        /// 检查是否有设备正在连接
        /// </summary>
        private bool IsAnyDeviceConnecting()
        {
            return TargetDevices.Values.Any(device => device.IsConnecting);
        }

        /// <summary>
        /// 开始设备连接
        /// </summary>
        private void StartDeviceConnection(BLEDeviceInfo deviceInfo)
        {
            deviceInfo.IsConnecting = true;
            deviceInfo.ConnectionStartTime = Time.time;

            Debug.Log($"开始连接设备: {deviceInfo.DeviceName}, 地址: {deviceInfo.DeviceAddress}");

            // 使用正确的ConnectToPeripheral方法签名
            BluetoothLEHardwareInterface.ConnectToPeripheral(deviceInfo.DeviceAddress, 
                null, // connectedAction - 不使用第一个连接回调
                null, // disconnectedAction - 不使用第一个断开回调
                (address, serviceUUID, characteristicUUID) => 
                {
                    // 服务和特征值发现回调
                    OnServiceCharacteristicDiscovered(deviceInfo, address, serviceUUID, characteristicUUID);
                },
                (address) => 
                {
                    // 断开连接回调
                    OnDeviceDisconnected(deviceInfo, address);
                });
        }

        /// <summary>
        /// 服务和特征值发现回调
        /// </summary>
        private void OnServiceCharacteristicDiscovered(BLEDeviceInfo deviceInfo, string address, string serviceUUID, string characteristicUUID)
        {
            if (deviceInfo.DeviceAddress != address) return;

            Debug.Log($"设备 {deviceInfo.DeviceName} 发现服务: {serviceUUID}, 特征: {characteristicUUID}");

            // 检查是否是我们需要的特征值
            if (IsEqual(serviceUUID, deviceInfo.ServiceNotifyUUID) &&
                IsEqual(characteristicUUID, deviceInfo.CharacteristicNotifyUUID))
            {
                Debug.Log($"设备 {deviceInfo.DeviceName} 找到目标特征值!");
                deviceInfo.HasFoundService = true;
                
                // 延迟一下再请求MTU，给设备一些处理时间
                UniTask.Create(async () =>
                {
                    await UniTask.Delay(100);
                    RequestDeviceMTU(deviceInfo);
                });
            }
        }

        /// <summary>
        /// 设备断开连接回调
        /// </summary>
        private void OnDeviceDisconnected(BLEDeviceInfo deviceInfo, string address)
        {
            if (deviceInfo.DeviceAddress != address) return;

            Debug.Log($"设备 {deviceInfo.DeviceName} 连接断开");
            deviceInfo.IsConnected = false;
            deviceInfo.IsConnecting = false;
            deviceInfo.HasFoundService = false;
            deviceInfo.HasSubscribed = false;
            ConnectedDevices.Remove(deviceInfo.DeviceName);

            // 如果还在扫描中，尝试重新连接
            if (_isScanning && TargetDevices.ContainsKey(deviceInfo.DeviceName))
            {
                Debug.Log($"计划重新连接设备: {deviceInfo.DeviceName}");
                UniTask.Create(async () =>
                {
                    await UniTask.Delay(3000); // 3秒后重连
                    if (_isScanning && !deviceInfo.IsConnected && !deviceInfo.IsConnecting)
                    {
                        _pendingConnectionQueue.Enqueue(deviceInfo.DeviceName);
                    }
                });
            }
        }

        /// <summary>
        /// 请求设备MTU
        /// </summary>
        private void RequestDeviceMTU(BLEDeviceInfo deviceInfo)
        {
            if (!deviceInfo.IsConnecting || !deviceInfo.HasFoundService) return;

            Debug.Log($"为设备 {deviceInfo.DeviceName} 请求MTU");
            
            BluetoothLEHardwareInterface.RequestMtu(deviceInfo.DeviceAddress, 23, (address, newMTU) =>
            {
                Debug.Log($"设备 {deviceInfo.DeviceName} MTU设置成功: {newMTU}");
                SubscribeToDevice(deviceInfo);
            });
        }

        /// <summary>
        /// 订阅设备数据
        /// </summary>
        private void SubscribeToDevice(BLEDeviceInfo deviceInfo)
        {
            if (!deviceInfo.IsConnecting) return;

            Debug.Log($"订阅设备 {deviceInfo.DeviceName} 的数据");

            BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(
                deviceInfo.DeviceAddress,
                deviceInfo.ServiceNotifyUUID,
                deviceInfo.CharacteristicNotifyUUID,
                (address, characteristic) => OnSubscriptionSuccess(deviceInfo, address, characteristic),
                (address, characteristicUUID, bytes) => OnCharacteristicValueChanged(deviceInfo, address, characteristicUUID, bytes));
        }

        /// <summary>
        /// 订阅成功回调
        /// </summary>
        private void OnSubscriptionSuccess(BLEDeviceInfo deviceInfo, string address, string characteristic)
        {
            if (deviceInfo.DeviceAddress != address) return;

            Debug.Log($"设备 {deviceInfo.DeviceName} 订阅成功");
            
            deviceInfo.IsConnected = true;
            deviceInfo.IsConnecting = false;
            deviceInfo.HasSubscribed = true;
            ConnectedDevices[deviceInfo.DeviceName] = deviceInfo;

            // 读取初始数据
            BluetoothLEHardwareInterface.ReadCharacteristic(deviceInfo.DeviceAddress,
                deviceInfo.ServiceNotifyUUID,
                deviceInfo.CharacteristicNotifyUUID,
                (characteristicUUID, bytes) => OnCharacteristicRead(deviceInfo, characteristicUUID, bytes));

            Debug.Log($"设备 {deviceInfo.DeviceName} 连接完成! 已连接设备: {ConnectedDevices.Count}/{TargetDevices.Count}");

            // 处理下一个连接
            ProcessConnectionQueue();

            // 检查是否所有设备都已连接
            if (AreAllTargetDevicesConnected())
            {
                Debug.Log("所有目标设备都已连接完成!");
                StopScan().Forget();
            }
        }

        /// <summary>
        /// 特征值读取回调
        /// </summary>
        private void OnCharacteristicRead(BLEDeviceInfo deviceInfo, string characteristicUUID, byte[] bytes)
        {
            try
            {
                deviceInfo.CacheBytes = bytes;
                ProcessData(deviceInfo.DeviceName, bytes);
                
                string dataStr = string.Join(" ", bytes.Select(b => b.ToString("X2")));
                Debug.Log($"设备 {deviceInfo.DeviceName} 初始数据: {dataStr}");
            }
            catch (Exception e)
            {
                Debug.LogError($"设备 {deviceInfo.DeviceName} 数据解析失败: {e}");
            }
        }

        /// <summary>
        /// 特征值变化回调
        /// </summary>
        private void OnCharacteristicValueChanged(BLEDeviceInfo deviceInfo, string address, string characteristicUUID, byte[] bytes)
        {
            if (deviceInfo.DeviceAddress != address) return;

            try
            {
                if (deviceInfo.CacheBytes == null)
                {
                    deviceInfo.CacheBytes = bytes;
                    ProcessData(deviceInfo.DeviceName, bytes);
                }
                else if (!deviceInfo.CacheBytes.SequenceEqual(bytes))
                {
                    ProcessData(deviceInfo.DeviceName, bytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"设备 {deviceInfo.DeviceName} 数据解析失败: {e}");
            }
        }

        /// <summary>
        /// 通过地址查找设备
        /// </summary>
        private BLEDeviceInfo FindDeviceByAddress(string deviceAddress)
        {
            return TargetDevices.Values.FirstOrDefault(device => device.DeviceAddress == deviceAddress);
        }

        // Update is called once per frame
        private void Update()
        {
            // 定期重启扫描以确保发现所有设备
            if (_isScanning && Time.time - _lastScanTime > SCAN_RESTART_INTERVAL)
            {
                if (GetUnconnectedDeviceCount() > 0)
                {
                    Debug.Log("重新启动扫描以寻找未连接的设备...");
                    BluetoothLEHardwareInterface.StopScan();
                    StartBluetoothScan();
                }
            }

            // 处理连接队列
            ProcessConnectionQueue();

            // 检查连接超时
            CheckConnectionTimeouts();
        }

        /// <summary>
        /// 检查连接超时
        /// </summary>
        private void CheckConnectionTimeouts()
        {
            foreach (var device in TargetDevices.Values)
            {
                if (device.IsConnecting && Time.time - device.ConnectionStartTime > 15f) // 15秒超时
                {
                    Debug.LogWarning($"设备 {device.DeviceName} 连接超时，重新加入队列");
                    device.IsConnecting = false;
                    device.HasFoundService = false;
                    _pendingConnectionQueue.Enqueue(device.DeviceName);
                }
            }
        }

        /// <summary>
        /// 设备发现回调
        /// </summary>
        private void OnDeviceDiscovered(string address, string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            foreach (var targetDevice in TargetDevices)
            {
                var deviceName = targetDevice.Key;
                var deviceInfo = targetDevice.Value;
                
                if (name.Contains(deviceName) && !deviceInfo.IsConnected && !deviceInfo.IsConnecting)
                {
                    if (string.IsNullOrEmpty(deviceInfo.DeviceAddress))
                    {
                        Debug.Log($"发现目标设备: {name}, 地址: {address}");
                        deviceInfo.DeviceAddress = address;
                        
                        // 添加到连接队列
                        _pendingConnectionQueue.Enqueue(deviceName);
                        Debug.Log($"设备 {deviceName} 添加到连接队列，队列长度: {_pendingConnectionQueue.Count}");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 设备发现回调（带数据）
        /// </summary>
        private void OnDeviceDiscoveredWithData(string address, string name, int rssi, byte[] bytes)
        {
            OnDeviceDiscovered(address, name);
        }

        static string FullUUID(string uuid)
        {
            return uuid.Length == 4 ? "0000" + uuid + "-0000-1000-8000-00805f9b34fb" : uuid;
        }

        private bool IsEqual(string uuid1, string uuid2)
        {
            if (uuid1.Length == 4)
                uuid1 = FullUUID(uuid1);
            if (uuid2.Length == 4)
                uuid2 = FullUUID(uuid2);

            return uuid1.ToUpper().Equals(uuid2.ToUpper());
        }

        /// <summary>
        /// 发送数据到指定设备
        /// </summary>
        public void SendDataToDevice(string deviceName, byte[] data)
        {
            if (ConnectedDevices.TryGetValue(deviceName, out var deviceInfo))
            {
                Debug.Log($"发送数据到设备: {deviceName}");
                BluetoothLEHardwareInterface.WriteCharacteristic(
                    deviceInfo.DeviceAddress,
                    deviceInfo.ServiceWriterUUID,
                    deviceInfo.CharacteristicWriterUUID,
                    data, data.Length, true,
                    (characteristicUUID) =>
                    {
                        Debug.Log($"设备 {deviceName} 数据发送成功");
                    });
            }
            else
            {
                Debug.LogWarning($"设备 {deviceName} 未连接，无法发送数据");
            }
        }

        /// <summary>
        /// 发送数据到所有连接的设备
        /// </summary>
        public void BroadcastData(byte[] data)
        {
            foreach (var device in ConnectedDevices)
            {
                SendDataToDevice(device.Key, data);
            }
        }

        /// <summary>
        /// 检查设备是否已连接
        /// </summary>
        public bool IsDeviceConnected(string deviceName)
        {
            return ConnectedDevices.ContainsKey(deviceName) && ConnectedDevices[deviceName].IsConnected;
        }

        /// <summary>
        /// 获取已连接设备列表
        /// </summary>
        public List<string> GetConnectedDevices()
        {
            return ConnectedDevices.Keys.ToList();
        }

        /// <summary>
        /// 获取设备连接状态
        /// </summary>
        public string GetDeviceStatus(string deviceName)
        {
            if (ConnectedDevices.ContainsKey(deviceName))
                return "已连接";
            else if (TargetDevices.ContainsKey(deviceName) && TargetDevices[deviceName].IsConnecting)
                return "连接中";
            else if (TargetDevices.ContainsKey(deviceName) && !string.IsNullOrEmpty(TargetDevices[deviceName].DeviceAddress))
                return "已发现";
            else if (TargetDevices.ContainsKey(deviceName))
                return "等待发现";
            else
                return "未配置";
        }
        
        /// <summary>
        /// 检查设备是否在目标设备列表中
        /// </summary>
        public bool IsTargetDevice(string deviceName)
        {
            return TargetDevices.ContainsKey(deviceName);
        }

        /// <summary>
        /// 获取设备连接状态
        /// </summary>
        public DeviceConnectionState GetDeviceConnectionState(string deviceName)
        {
            if (ConnectedDevices.ContainsKey(deviceName))
                return DeviceConnectionState.Connected;
            else if (TargetDevices.ContainsKey(deviceName) && TargetDevices[deviceName].IsConnecting)
                return DeviceConnectionState.Connecting;
            else if (TargetDevices.ContainsKey(deviceName) && !string.IsNullOrEmpty(TargetDevices[deviceName].DeviceAddress))
                return DeviceConnectionState.Discovered;
            else if (TargetDevices.ContainsKey(deviceName))
                return DeviceConnectionState.Pending;
            else
                return DeviceConnectionState.NotConfigured;
        }

        public enum DeviceConnectionState
        {
            NotConfigured,
            Pending,
            Discovered,
            Connecting,
            Connected
        }
        
        /// <summary>
        /// 场景切换时保持设备连接
        /// </summary>
        public void MaintainDeviceConnectionsOnSceneChange()
        {
            // 保持所有已连接设备的连接状态
            foreach (var device in ConnectedDevices.Values.ToList())
            {
                Debug.Log($"场景切换，保持设备连接: {device.DeviceName}");
            }
    
            // 继续扫描未连接的设备
            if (GetUnconnectedDeviceCount() > 0)
            {
                _isScanning = true;
                StartBluetoothScan();
            }
        }

        /// <summary>
        /// 安全添加设备（如果不存在）
        /// </summary>
        public void SafeAddTargetDevice(string deviceName, string serviceNotifyUUID = "fff0", 
            string serviceWriterUUID = "fff0", string characteristicNotifyUUID = "fff1", 
            string characteristicWriterUUID = "fff2")
        {
            if (!TargetDevices.ContainsKey(deviceName))
            {
                AddTargetDevice(deviceName, serviceNotifyUUID, serviceWriterUUID, characteristicNotifyUUID, characteristicWriterUUID);
            }
            else
            {
                // 更新设备信息（如果需要）
                var deviceInfo = TargetDevices[deviceName];
                deviceInfo.ServiceNotifyUUID = serviceNotifyUUID;
                deviceInfo.ServiceWriterUUID = serviceWriterUUID;
                deviceInfo.CharacteristicNotifyUUID = characteristicNotifyUUID;
                deviceInfo.CharacteristicWriterUUID = characteristicWriterUUID;
            }
        }
        
        /// <summary>
        /// 优先级连接设备
        /// </summary>
        public void ConnectDeviceWithPriority(string deviceName, int priority = 0)
        {
            if (!TargetDevices.ContainsKey(deviceName)) return;
    
            var deviceInfo = TargetDevices[deviceName];
            if (!string.IsNullOrEmpty(deviceInfo.DeviceAddress) && 
                !deviceInfo.IsConnected && !deviceInfo.IsConnecting)
            {
                // 根据优先级插入队列
                if (priority == 0)
                {
                    _pendingConnectionQueue.Enqueue(deviceName);
                }
                else
                {
                    // 高优先级设备插入队列前端
                    var tempQueue = new Queue<string>();
                    tempQueue.Enqueue(deviceName);
                    while (_pendingConnectionQueue.Count > 0)
                    {
                        tempQueue.Enqueue(_pendingConnectionQueue.Dequeue());
                    }
                    _pendingConnectionQueue = tempQueue;
                }
            }
        }

        /// <summary>
        /// 连接所有未连接的设备
        /// </summary>
        public void ConnectAllPendingDevices()
        {
            foreach (var device in TargetDevices.Values)
            {
                if (!device.IsConnected && !device.IsConnecting && 
                    !string.IsNullOrEmpty(device.DeviceAddress))
                {
                    _pendingConnectionQueue.Enqueue(device.DeviceName);
                }
            }
    
            ProcessConnectionQueue();
        }
        
        /// <summary>
        /// 可靠的心跳发送
        /// </summary>
        public async UniTask<bool> SendReliableHeartbeat(string deviceName, byte[] data, int maxRetries = 2)
        {
            for (int retry = 0; retry <= maxRetries; retry++)
            {
                try
                {
                    if (IsDeviceConnected(deviceName))
                    {
                        SendDataToDevice(deviceName, data);
                        Debug.Log($"成功发送心跳到 {deviceName}");
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"设备 {deviceName} 未连接，尝试重新连接 (重试 {retry + 1}/{maxRetries + 1})");
                
                        // 确保设备在目标列表中
                        if (!IsTargetDevice(deviceName))
                        {
                            // 重新添加设备
                            AddTargetDevice(deviceName, 
                                BLEConst.ServiceNotifyUUID_BallCamera,
                                BLEConst.ServiceWriterUUID_BallCamera,
                                BLEConst.CharacteristicNotifyUUID_BallCamera,
                                BLEConst.CharacteristicWriterUUID_BallCamera);
                        }
                
                        // 尝试重新连接
                        ConnectDeviceWithPriority(deviceName, 1); // 高优先级
                
                        // 等待连接
                        await UniTask.Delay(2000);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"发送心跳到 {deviceName} 失败 (重试 {retry + 1}): {e}");
                }
            }
    
            Debug.LogError($"发送心跳到 {deviceName} 最终失败");
            return false;
        }
    }
}