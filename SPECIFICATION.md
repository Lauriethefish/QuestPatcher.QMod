# QMOD Format Specification v1.1.0

Outlined below are the requirements for a valid version 1.1.0 QMOD file:
- A QMOD file MUST be a valid ZIP file, according to the PKWARE ZIP specification, version 6.3.10.
- A QMOD file MUST NOT use the ZIP64 format.
- A QMOD file MUST contain **exactly one** entry with full name `mod.json`.
- The `mod.json` entry MUST contain valid JSON (ECMA-404) data, in UTF-8 encoding.
- The JSON data in the `mod.json` MUST comply with the [mod.json schema](./QuestPatcher.QMod/Resources/qmod.schema.json).

The below are strong suggestions for the values of certain properties in the `mod.json`. They are not strictly required, but SHOULD be adhered to.
- The `coverImage` property, if present, SHOULD contain the full name of an entry in the ZIP, containing the mod's cover image. The format of the image file is not specified, but the most common formats used are PNG, JPEG and GIF.

- Strings in the `modFiles`, `lateModFiles` and `libraryFiles` lists SHOULD be full name of an entry in the ZIP.
- The `name` property of objects in the `fileCopies` list SHOULD be the full name of an entry in the ZIP.