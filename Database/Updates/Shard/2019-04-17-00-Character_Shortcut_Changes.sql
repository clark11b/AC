USE `ace_shard`;

ALTER TABLE `character_properties_shortcut_bar` 
DROP FOREIGN KEY `wcid_shortcutbar`;
ALTER TABLE `character_properties_shortcut_bar` 
DROP COLUMN `id`,
DROP PRIMARY KEY,
ADD PRIMARY KEY (`character_Id`, `shortcut_Bar_Index`),
ADD INDEX `wcid_shortcutbar_idx` (`character_Id` ASC),
DROP INDEX `wcid_shortcutbar_barIndex_ObjectId_uidx` ;
;
ALTER TABLE `character_properties_shortcut_bar` 
ADD CONSTRAINT `wcid_shortcutbar`
  FOREIGN KEY (`character_Id`)
  REFERENCES `character` (`id`)
  ON DELETE CASCADE;
