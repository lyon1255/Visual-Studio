namespace GnosisRealmCore.Infrastructure;

public static class ServiceRoles
{
    public const string NodeRegister = "node.register";
    public const string NodeHeartbeat = "node.heartbeat";
    public const string NodeCommandsRead = "node.commands.read";
    public const string ZoneHeartbeatWrite = "zone.heartbeat.write";
    public const string ZoneCharacterSave = "zone.character.save";
    public const string RealmGameDataRead = "realm.gamedata.read";
}
