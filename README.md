# DiscordWhoIs — User Guide

This file explains the bot's commands and how to use them.

---

## What the bot does

DiscordWhoIs links Discord users to AO3 (Archive of Our Own) author profiles and fanfics. The following are the features that are present within the bot:

- Register which AO3 author belongs to a Discord user.
- Add a short or multi-line description for an author.
- Manage alias mappings (alternate AO3 usernames) so searches find the correct author.
- Look up authors by AO3 name, alias, FanFic.net name, or Discord username.

---

## Commands (user-facing)

Below are the main slash commands available as well as examples and descriptions on how they work.

1) Register an AO3 author to a Discord account

`/ao3-register <Ao3-Username> [Discord-User]`

Description: Claim an AO3 author name for a Discord user. If you specify a `Discord-User` then it will associate to that `Discord User` otherwise it'll associate with the calling `Discord User`. Can only be used in a server (guild). Requires server admin permissions to register for another user.

Examples:
- Register yourself: `/ao3-register example_author`
- Admin registers for someone else: `/ao3-register example_author @SomeUser`

What to expect:
- If the AO3 name isn't present in the bot's database (no fanfics registered), the bot will ask you to publish at least one fanfic first.
- You must be in a server (guild) to use this command; DMs are not supported.
- Only server admins can register an AO3 author for another Discord user.
- If the Discord user is already registered to another AO3 author, you'll be warned. 
- Admins may override existing registrations when using the `Discord-User` parameter.

2) Add or update an author description

`/author-description [description] [discord-user]`

Description: Allows adding or updating a description for an AO3 author linked to a Discord user. If you specify a `discord-user`, it updates that user's author description; otherwise, it updates your own. Requires server admin permissions to update another user's description. If you specify a description, it will be set directly; if left empty, a modal popup will appear for multi-line input.

Examples:
- Quick single-line: `/author-description "Writes cozy slice-of-life fics"`
- Multi-line: `/author-description` (then complete the popup modal)
- Register description for another user (admin): `/author-description "Fandom archivist" @OtherUser`

What to expect:
- You must be registered to an AO3 author to set your own description.
- You must be in a server (guild) to use this command; DMs are not supported.
- You cannot set a description for another user unless you have server admin permissions.
- If the target Discord user isn't registered to an AO3 author, you'll be informed.
- If you provide a description in the command, it will be set directly.
- If you leave the description empty, a modal will appear for you to enter a multi-line description.
- Only server admins can update another user's author description.

3) Manage AO3 aliases

Commands are grouped under `/alias`. Requires server admin permissions.

- Add or update an alias: `/alias add <alias> <Ao3-Username>`
  - Example: `/alias add alt_username example_author`

- Remove an alias: `/alias remove <alias>`
  - Example: `/alias remove alt_username`

- List aliases: `/alias list`
  - Example: `/alias list` (bot replies with configured alias -> canonical name pairs)

Aliases let you map alternate AO3 usernames to the canonical author name so searches find the right person.

What to expect:
- You must be in a server (guild) to use these commands; DMs are not supported.
- Only server admins can manage aliases.
- When adding an alias, if it already exists, it will update to point to the new AO3 author.
- When removing an alias, if it doesn't exist, you'll be informed.
- The list command will show all configured aliases in the server.
- Aliases are server-specific; they do not apply globally across all servers.

4) Lookup an author (`/who-is-author`)

`/who-is-author <query>`

Description: Search for an author. You can search by AO3 username, configured alias, FanFic.net name, or Discord username.

Examples:
- `/who-is-author example_author`
- `/who-is-author alt_username` (if `alt_username` is configured as an alias)
- `/who-is-author SomeDiscordUser`

What to expect:
- This command can be used in DMs or in a server (guild).
- This command searches by AO3 username, alias, FanFic.net name, or Discord username.
- The bot will search its database for matches based on the provided username or alias.
- If multiple matches are found, the bot will take the first one. 
- If no matches are found, the bot will inform you.
- The bot will return the AO3 author profile link, total number of fanfics, kudos, a description (if set), and a list of the ten most recent fanfics.

Tips:
- Searches are case-insensitive.
- If you don't find a result, try common variants or check with `/alias list` to see configured aliases.

---

## Permissions and behavior notes

- Most commands must be used in a server (guild). The bot will reject DMs for these commands.
- Some actions require server admin permissions (for example, registering someone else or removing aliases). The bot will indicate if you lack permission.
- When registering or changing ownership, the bot warns if the target Discord user is already associated with another AO3 author. Admins can override with the proper parameters.

---

## Quick troubleshooting

- Not finding an author: try searching by a known alias, Discord username, or FanFic.net name.
- Want fewer missed matches: server admins can configure aliases using `/alias add` to cover alternate spellings.

---
