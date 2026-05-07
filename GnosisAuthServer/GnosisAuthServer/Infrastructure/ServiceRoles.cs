namespace GnosisAuthServer.Infrastructure;

public static class ServiceRoles
{
    public const string OfficialRealmHeartbeatWrite = "official-realm-heartbeat.write";
    public const string CommunityRealmHeartbeatWrite = "community-realm-heartbeat.write";
    public const string RealmGameDataRead = "realm-gamedata.read";
    public const string RealmSchemaRead = "realm-schema.read";
}