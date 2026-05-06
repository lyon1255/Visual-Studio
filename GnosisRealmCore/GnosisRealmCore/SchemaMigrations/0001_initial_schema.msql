CREATE TABLE IF NOT EXISTS characters (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    steam_id VARCHAR(50) NOT NULL,
    name VARCHAR(32) NOT NULL,
    is_banned TINYINT(1) NOT NULL DEFAULT 0,
    currency BIGINT NOT NULL DEFAULT 0,
    class_type INT NOT NULL DEFAULT 0,
    level INT NOT NULL DEFAULT 1,
    experience INT NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    current_hp FLOAT NOT NULL DEFAULT 100,
    current_mp FLOAT NOT NULL DEFAULT 100,
    last_zone VARCHAR(50) NOT NULL DEFAULT 'City',
    last_pos_x FLOAT NOT NULL DEFAULT 0,
    last_pos_y FLOAT NOT NULL DEFAULT 0,
    last_pos_z FLOAT NOT NULL DEFAULT 0,
    last_rot_y FLOAT NOT NULL DEFAULT 0,
    is_online TINYINT(1) NOT NULL DEFAULT 0,
    last_logout DATETIME NULL,
    guild_id INT NULL,
    UNIQUE KEY ux_characters_name (name),
    KEY ix_characters_steam_id (steam_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_equipment (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    slot_type INT NOT NULL,
    item_id VARCHAR(255) NOT NULL,
    amount INT NOT NULL DEFAULT 1,
    KEY ix_character_equipment_character_id_slot_type (character_id, slot_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_hotbar (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    slot_index INT NOT NULL,
    shortcut_type INT NOT NULL,
    shortcut_id VARCHAR(50) NOT NULL,
    UNIQUE KEY ux_character_hotbar_character_slot (character_id, slot_index)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_items (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    container_type TINYINT NOT NULL DEFAULT 0,
    slot_index INT NOT NULL,
    item_id VARCHAR(50) NOT NULL,
    amount INT NOT NULL DEFAULT 1,
    item_level INT NOT NULL DEFAULT 1,
    current_durability INT NOT NULL DEFAULT 0,
    is_bound TINYINT(1) NOT NULL DEFAULT 0,
    is_locked TINYINT(1) NOT NULL DEFAULT 0,
    upgrade_level TINYINT UNSIGNED NOT NULL DEFAULT 0,
    enchant_id VARCHAR(255) NOT NULL DEFAULT '',
    transmog_id VARCHAR(255) NOT NULL DEFAULT '',
    crafted_by VARCHAR(255) NOT NULL DEFAULT '',
    KEY ix_character_items_character_container_slot (character_id, container_type, slot_index)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_mail (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    receiver_id INT NOT NULL,
    sender_name VARCHAR(50) NULL DEFAULT 'System',
    subject VARCHAR(100) NULL,
    message TEXT NULL,
    attached_item_id VARCHAR(100) NULL,
    attached_amount INT NULL DEFAULT 0,
    attached_currency BIGINT NULL DEFAULT 0,
    is_read TINYINT(1) NULL DEFAULT 0,
    sent_at DATETIME NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_quests (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    quest_id VARCHAR(255) NOT NULL,
    progress VARCHAR(255) NOT NULL DEFAULT '0',
    status INT NOT NULL DEFAULT 0,
    is_daily TINYINT(1) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_quest_history (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    quest_id VARCHAR(255) NOT NULL,
    completed_at DATETIME NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY ux_character_quest_history_character_quest (character_id, quest_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_settings (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    setting_key VARCHAR(100) NOT NULL,
    setting_value TEXT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS character_social (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    target_id INT NOT NULL,
    relation_type INT NOT NULL DEFAULT 0,
    UNIQUE KEY ux_character_social_character_target (character_id, target_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS class_stats (
    class_type INT NOT NULL PRIMARY KEY,
    class_name VARCHAR(32) NOT NULL,
    base_max_health FLOAT NOT NULL,
    base_max_mana FLOAT NOT NULL,
    base_ad FLOAT NOT NULL,
    base_ap FLOAT NOT NULL,
    base_defense FLOAT NOT NULL,
    base_attack_speed FLOAT NOT NULL,
    base_crit_chance FLOAT NOT NULL,
    hp_per_level FLOAT NOT NULL,
    mana_per_level FLOAT NOT NULL,
    ad_per_level FLOAT NOT NULL,
    ap_per_level FLOAT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS guilds (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    leader_id INT NOT NULL,
    level INT NOT NULL DEFAULT 1,
    motd VARCHAR(255) NULL DEFAULT 'Welcome to the guild!'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS permissions (
    steam_id VARCHAR(25) NOT NULL PRIMARY KEY,
    permission_level INT NOT NULL DEFAULT 0,
    note VARCHAR(255) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS realm_nodes (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    node_id VARCHAR(64) NOT NULL,
    name VARCHAR(50) NOT NULL DEFAULT 'Node',
    api_url VARCHAR(255) NOT NULL,
    api_key VARCHAR(255) NOT NULL DEFAULT '',
    max_zones INT NOT NULL DEFAULT 10,
    active_zones INT NOT NULL DEFAULT 0,
    public_ip VARCHAR(64) NOT NULL DEFAULT '127.0.0.1',
    status VARCHAR(16) NOT NULL DEFAULT 'online',
    last_heartbeat_utc DATETIME NULL,
    UNIQUE KEY ux_realm_nodes_node_id (node_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS realm_zone_instances (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    zone_name VARCHAR(64) NOT NULL,
    node_id VARCHAR(64) NOT NULL,
    ip_address VARCHAR(64) NOT NULL DEFAULT '127.0.0.1',
    port INT NOT NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'starting',
    current_players INT NOT NULL DEFAULT 0,
    max_players INT NOT NULL DEFAULT 0,
    started_at_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_heartbeat_utc DATETIME NULL,
    KEY ix_realm_zone_instances_zone_name (zone_name),
    KEY ix_realm_zone_instances_node_id (node_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS realm_node_commands (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    node_id VARCHAR(64) NOT NULL,
    command_type VARCHAR(32) NOT NULL,
    payload_json LONGTEXT NOT NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'pending',
    created_at_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    claimed_at_utc DATETIME NULL,
    completed_at_utc DATETIME NULL,
    error_text VARCHAR(1024) NULL,
    KEY ix_realm_node_commands_lookup (node_id, status, created_at_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS realm_game_data_overrides (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    category VARCHAR(32) NOT NULL,
    asset_id VARCHAR(128) NOT NULL,
    class_type VARCHAR(128) NOT NULL,
    json_data LONGTEXT NOT NULL,
    override_action VARCHAR(16) NOT NULL DEFAULT 'override',
    is_enabled TINYINT(1) NOT NULL DEFAULT 1,
    updated_at_utc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY ux_realm_game_data_override_category_asset (category, asset_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS server_rates (
    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    xp_multiplier FLOAT NOT NULL DEFAULT 1,
    drop_rate_multiplier FLOAT NOT NULL DEFAULT 1,
    gold_multiplier FLOAT NOT NULL DEFAULT 1
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO server_rates (id, xp_multiplier, drop_rate_multiplier, gold_multiplier)
SELECT 1, 1, 1, 1
WHERE NOT EXISTS (SELECT 1 FROM server_rates WHERE id = 1);

INSERT INTO class_stats (class_type, class_name, base_max_health, base_max_mana, base_ad, base_ap, base_defense, base_attack_speed, base_crit_chance, hp_per_level, mana_per_level, ad_per_level, ap_per_level)
SELECT 0, 'Warrior', 150, 50, 12, 0, 15, 1.2, 0.05, 25, 5, 2, 0
WHERE NOT EXISTS (SELECT 1 FROM class_stats WHERE class_type = 0);

INSERT INTO class_stats (class_type, class_name, base_max_health, base_max_mana, base_ad, base_ap, base_defense, base_attack_speed, base_crit_chance, hp_per_level, mana_per_level, ad_per_level, ap_per_level)
SELECT 1, 'Mage', 80, 200, 4, 15, 5, 1, 0.05, 12, 30, 0.5, 3
WHERE NOT EXISTS (SELECT 1 FROM class_stats WHERE class_type = 1);

INSERT INTO class_stats (class_type, class_name, base_max_health, base_max_mana, base_ad, base_ap, base_defense, base_attack_speed, base_crit_chance, hp_per_level, mana_per_level, ad_per_level, ap_per_level)
SELECT 2, 'Rogue', 100, 80, 10, 0, 8, 1.6, 0.15, 18, 8, 1.5, 0
WHERE NOT EXISTS (SELECT 1 FROM class_stats WHERE class_type = 2);

INSERT INTO class_stats (class_type, class_name, base_max_health, base_max_mana, base_ad, base_ap, base_defense, base_attack_speed, base_crit_chance, hp_per_level, mana_per_level, ad_per_level, ap_per_level)
SELECT 3, 'Priest', 90, 180, 5, 10, 6, 1, 0.05, 15, 25, 1, 2
WHERE NOT EXISTS (SELECT 1 FROM class_stats WHERE class_type = 3);
