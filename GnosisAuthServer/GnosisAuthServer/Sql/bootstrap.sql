CREATE TABLE IF NOT EXISTS accounts (
    id INT NOT NULL AUTO_INCREMENT,
    steam_id VARCHAR(32) NOT NULL,
    is_banned BIT NOT NULL DEFAULT 0,
    ban_reason VARCHAR(256) NULL,
    account_type VARCHAR(32) NOT NULL DEFAULT 'player',
    created_at DATETIME NOT NULL,
    last_login_at DATETIME NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_accounts_steam_id (steam_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS realms (
    id INT NOT NULL AUTO_INCREMENT,
    realm_id VARCHAR(64) NOT NULL,
    display_name VARCHAR(128) NOT NULL,
    region VARCHAR(32) NOT NULL,
    kind VARCHAR(16) NOT NULL,
    status VARCHAR(16) NOT NULL,
    public_base_url VARCHAR(256) NOT NULL,
    current_players INT NOT NULL DEFAULT 0,
    max_players INT NOT NULL DEFAULT 0,
    healthy_zone_count INT NOT NULL DEFAULT 0,
    is_listed BIT NOT NULL DEFAULT 1,
    is_official BIT NOT NULL DEFAULT 1,
    last_heartbeat_at DATETIME NULL,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_realms_realm_id (realm_id),
    KEY ix_realms_list_status_heartbeat (is_listed, status, last_heartbeat_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS gamedata_prefabs (
    id INT NOT NULL AUTO_INCREMENT,
    asset_id VARCHAR(100) NOT NULL,
    class_type VARCHAR(100) NOT NULL,
    json_data LONGTEXT NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    last_updated DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_gamedata_prefabs_asset_id (asset_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS gamedata_items (
    id INT NOT NULL AUTO_INCREMENT,
    asset_id VARCHAR(100) NOT NULL,
    class_type VARCHAR(100) NOT NULL,
    json_data LONGTEXT NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    last_updated DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_gamedata_items_asset_id (asset_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS gamedata_entities (
    id INT NOT NULL AUTO_INCREMENT,
    asset_id VARCHAR(100) NOT NULL,
    class_type VARCHAR(100) NOT NULL,
    json_data LONGTEXT NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    last_updated DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_gamedata_entities_asset_id (asset_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS gamedata_quests (
    id INT NOT NULL AUTO_INCREMENT,
    asset_id VARCHAR(100) NOT NULL,
    class_type VARCHAR(100) NOT NULL,
    json_data LONGTEXT NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    last_updated DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_gamedata_quests_asset_id (asset_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS gamedata_spells (
    id INT NOT NULL AUTO_INCREMENT,
    asset_id VARCHAR(100) NOT NULL,
    class_type VARCHAR(100) NOT NULL,
    json_data LONGTEXT NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    last_updated DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_gamedata_spells_asset_id (asset_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS gamedata_auras (
    id INT NOT NULL AUTO_INCREMENT,
    asset_id VARCHAR(100) NOT NULL,
    class_type VARCHAR(100) NOT NULL,
    json_data LONGTEXT NOT NULL,
    is_enabled BIT NOT NULL DEFAULT 1,
    last_updated DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_gamedata_auras_asset_id (asset_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS gamedata_versions (
    id INT NOT NULL AUTO_INCREMENT,
    version_number INT NOT NULL,
    version_tag VARCHAR(64) NOT NULL,
    content_hash VARCHAR(128) NOT NULL,
    notes VARCHAR(512) NULL,
    published_at DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY ux_gamedata_versions_version_number (version_number),
    KEY ix_gamedata_versions_published_at (published_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS banned_ip_addresses (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    ip_address VARCHAR(64) NOT NULL,
    reason VARCHAR(256) NULL,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    created_at_utc DATETIME NOT NULL,
    expires_at_utc DATETIME NULL,
    UNIQUE KEY uq_banned_ip_addresses_ip_address (ip_address)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
