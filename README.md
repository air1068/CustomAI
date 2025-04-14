# CustomAI
Based on AnnaBannana's classic system, CustomAI allows you to create custom combat AIs for party members by writing lists of instructions. Middle-click on a party member and choose "Edit CustomAI" to open the editor interface.

## Basics
* Each instruction in the list is checked from top to bottom, and the first one with a valid condition is executed.
* By default, the action will be used on the pet's current enemy. If, for example, you want a healing spell to be cast on you when your health is low, be sure to tick the "Entity=Target" box so that the entity ("Player") gets healed and not the enemy.
* You can use "And" to check multiple conditions before taking a specific action. (For example, checking the pet's MP and the target's distance before casting a touch spell.)
* "-" deletes an instruction from the list, and "+" adds a new one. If you delete all instructions, the pet will go back to the vanilla combat AI.
* HP, MP, and SP can use a specific number or a percentage. For example, you can check if a pet has at least 10 MP, or if they're below 50% HP.
* Summons uses a specific number, but as a special case it can also use "100%" to mean the entity's summon cap.
* Status uses the alias of a condition, but name and name_JP will also work. (A list of aliases can be found here: https://docs.google.com/spreadsheets/d/16-LkHtVqjuN9U0rripjBn-nYwyqqSGg_/edit?gid=2127729747#gid=2127729747 ) In addition, Status can use "Spiky" and "Suicide Bomb" to check for the presence of the Spiky feat and the Suicide Bomb special action, respectively.

## Planned Future Updates
* Add the "Ally" entity.
* Add a "Random" condition, based on the "Percent Chance" condition added to Custom-GX by JianmengYu.
* Add an import/export system.
* Open the editor interface via dialogue option instead of middle-clicking.

## Other Notes
* I don't intend to recreate AnnaBannana's ability teaching system. That should be a separate Elin mod.
* This mod won't change vanilla game mechanics. For example, pets with the Blind status still move in a random direction, and pets with the Fear status still won't move into melee range of their target.
* Make sure that YK Framework ( https://steamcommunity.com/workshop/filedetails/?id=3400020753 ) is above this mod in the load order! (You can rearrange your mods' load order in the Mod Viewer accessed from the title screen.)
