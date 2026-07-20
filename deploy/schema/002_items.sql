CREATE TABLE IF NOT EXISTS item_instances (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    owner_steam_id BIGINT UNSIGNED NOT NULL,
    item_key VARCHAR(64) NOT NULL,
    rarity VARCHAR(16) NOT NULL,
    enhance_level INT NOT NULL DEFAULT 0,
    affixes JSON NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_owner (owner_steam_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
