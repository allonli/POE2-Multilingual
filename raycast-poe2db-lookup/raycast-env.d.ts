/// <reference types="@raycast/api">

/* 🚧 🚧 🚧
 * This file is auto-generated from the extension's manifest.
 * Do not modify manually. Instead, update the `package.json` file.
 * 🚧 🚧 🚧 */

/* eslint-disable @typescript-eslint/ban-types */

type ExtensionPreferences = {}

/** Preferences accessible in all the extension's commands */
declare type Preferences = ExtensionPreferences

declare namespace Preferences {
  /** Preferences accessible in the `search` command */
  export type Search = ExtensionPreferences & {
  /** undefined - Read selected text from the previous app when the command opens. Keep disabled for games to avoid focus flicker. */
  "prefillSelectedText": boolean
}
  /** Preferences accessible in the `refresh` command */
  export type Refresh = ExtensionPreferences & {}
}

declare namespace Arguments {
  /** Arguments passed to the `search` command */
  export type Search = {}
  /** Arguments passed to the `refresh` command */
  export type Refresh = {}
}

