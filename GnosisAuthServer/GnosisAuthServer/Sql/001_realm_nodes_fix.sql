START TRANSACTION;

CREATE TABLE IF NOT EXISTS `schema_migrations` (
    `version` INT NOT NULL,
    `description` VARCHAR(255) NOT NULL,
    `applied_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `realm_nodes` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `realm_id` VARCHAR(64) NOT NULL,
    `name` VARCHAR(100) NOT NULL,
    `region` VARCHAR(32) NOT NULL,
    `public_base_url` VARCHAR(255) NOT NULL,
    `internal_base_url` VARCHAR(255) DEFAULT NULL,
    `service_secret_hash` VARCHAR(255) NOT NULL,
    `enabled` TINYINT(1) NOT NULL DEFAULT 1,
    `status` VARCHAR(32) NOT NULL DEFAULT 'offline',
    `current_players` INT NOT NULL DEFAULT 0,
    `max_players` INT NOT NULL DEFAULT 0,
    `build_version` VARCHAR(32) DEFAULT NULL,
    `protocol_version` INT NOT NULL DEFAULT 1,
    `last_heartbeat_utc` DATETIME NULL,
    `created_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_realm_nodes_realm_id` (`realm_id`),
    KEY `ix_realm_nodes_enabled` (`enabled`),
    KEY `ix_realm_nodes_status` (`status`),
    KEY `ix_realm_nodes_last_heartbeat_utc` (`last_heartbeat_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

SET @db_name = DATABASE();

SET @has_enabled = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @db_name
      AND TABLE_NAME = 'realm_nodes'
      AND COLUMN_NAME = 'enabled'
);

SET @sql = IF(
    @has_enabled = 0,
    'ALTER TABLE `realm_nodes` ADD COLUMN `enabled` TINYINT(1) NOT NULL DEFAULT 1 AFTER `service_secret_hash`;',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_created_at_utc = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @db_name
      AND TABLE_NAME = 'realm_nodes'
      AND COLUMN_NAME = 'created_at_utc'
);

SET @sql = IF(
    @has_created_at_utc = 0,
    'ALTER TABLE `realm_nodes` ADD COLUMN `created_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER `last_heartbeat_utc`;',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_updated_at_utc = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @db_name
      AND TABLE_NAME = 'realm_nodes'
      AND COLUMN_NAME = 'updated_at_utc'
);

SET @sql = IF(
    @has_updated_at_utc = 0,
    'ALTER TABLE `realm_nodes` ADD COLUMN `updated_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER `created_at_utc`;',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_protocol_version = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @db_name
      AND TABLE_NAME = 'realm_nodes'
      AND COLUMN_NAME = 'protocol_version'
);

SET @sql = IF(
    @has_protocol_version = 0,
    'ALTER TABLE `realm_nodes` ADD COLUMN `protocol_version` INT NOT NULL DEFAULT 1 AFTER `build_version`;',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_unique_realm_id = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = @db_name
      AND TABLE_NAME = 'realm_nodes'
      AND INDEX_NAME = 'uq_realm_nodes_realm_id'
);

SET @sql = IF(
    @has_unique_realm_id = 0,
    'ALTER TABLE `realm_nodes` ADD UNIQUE KEY `uq_realm_nodes_realm_id` (`realm_id`);',
    'SELECT 1;'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT IGNORE INTO `schema_migrations` (`version`, `description`)
VALUES (1, 'Create or patch realm_nodes with enabled and audit columns');

COMMIT;