/// <summary>
/// 蓝牙连接配置常量
/// </summary>
public class BLEConst
{
    //摄像机蓝牙配置数据
    public const string DeviceName_BallCamera = "TTHost2";
    public const string ServiceNotifyUUID_BallCamera = "fff0";
    public const string ServiceWriterUUID_BallCamera = "fff0";
    public const string CharacteristicNotifyUUID_BallCamera = "fff1";
    public const string CharacteristicWriterUUID_BallCamera = "fff2";
    
    //发球机蓝牙配置数据
    public const string DeviceName_BallMachine1 = "Robot";//Roboti10_1
    public const string DeviceName_BallMachine2 = "Robot";//Roboti10_2
    public const string ServiceNotifyUUID_BallMachine = "fff0";
    public const string ServiceWriterUUID_BallMachine = "fff0";
    public const string CharacteristicNotifyUUID_BallMachine = "fff1";
    public const string CharacteristicWriterUUID_BallMachine = "fff2";
    
    //标靶摄像头蓝牙
    public const string DeviceName_TargetCamera2 = "Tennis_2";
    public const string ServiceNotifyUUID_TargetCamera2 = "ffe0";
    public const string ServiceWriterUUID_TargetCamera2= "ffe5";
    public const string CharacteristicNotifyUUID_TargetCamera2 = "ffe4";
    public const string CharacteristicWriterUUID_TargetCamera2 = "ffe9";
    
    //测速器
    public const string DeviceName_MySpeeds1 = "Myspeedz";
    public const string DeviceName_MySpeeds2 = "Myspeedz_2";
    public const string ServiceNotifyUUID_MySpeeds = "ffe0";
    public const string CharacteristicNotifyUUID_MySpeeds = "ffe4";
}
