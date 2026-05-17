---
tags:
  - type/doc
  - project/parselord5
  - status/active
type: doc
project: parselord5
status: active
aliases: []
---
# Healing Settings

> [!Note]
> If you choose to use Auto Rotation, see that guide first.

> [!Note]
> If you are not using Auto Rotation, see below for Heal Stack retargeting setup.

Healing in WrathCombo works by replacing your primary single-target and AoE healing buttons with additional spells
in an order determined by your settings. It is more involved than DPS setup and usually takes some adjustment to
dial in the behavior you want.

> [!Tip]
> This is best done in Duty Support. Adjust settings between, or even during, pulls. Scions never complain.

## Understanding Priorities

- Every heal has a priority selector that controls where it falls in the list.
  - The lower the priority number, the sooner it is checked.
- Every heal also has a health threshold that controls when that spell can be used.
  - If you are using Auto Rotation, the healing combo still will not activate until the Auto Rotation threshold is met.
- Other limiting options, such as "only weave" and "not on bosses", further restrict when a spell is used.

> [!Tip]
> "Not on bosses" is used on many AoE spells in the single-target combo so they can still be used on dungeon trash.

## How It Works

- A target falls below the Auto Rotation healing threshold, so WrathCombo switches to the single-target or AoE healing combo based on how many targets need healing.
- WrathCombo checks your priority 1 spell first. If it matches the threshold and options you set, it will use it. If not, it skips to the next spell.
- WrathCombo proceeds down the list by priority to determine which spell to use.
- If no spell in the list matches, WrathCombo uses the default spell that the button is replacing, such as Medica or Cure II.

> [!Tip]
> Knowing how this works, you can see how it would make sense to set something like Benediction
> to a low health threshold but priority number 1 so that if it is ever needed, it fires immediately.

# Heal Stack (Retargeting)

With the introduction of retargeting, you no longer need an additional mod such as ReactionEx or Redirect to
set up custom targeting such as mouseover. When you mouse over your target, WrathCombo uses that target's health for healing thresholds.

## The Settings

You can find retargeting settings from the "Settings" button in the left column. Scroll down to Targeting Options.
This section has options for enabling retargeting, plus the Heal Stack and Raise Stack.

> [!Tip]
> Stack refers to target priority for your spells. For example: use mouseover first, then hard target, then self.

### Customizing the Stacks

Your current Heal Stack is displayed in the settings. Expand the Heal Stack customization dropdown to add options
such as mouseover. The display updates to show your target priority.

>[!Tip]
> If you are still using another retargeting mod with its own stacks, use 
> these settings or the custom heal stack to match your other mods' setups. This is 
> so when WrathCombo checks a target's health, it checks the target you expect.

You can also customize the Raise Stack. If you choose any retargeted raise features in WrathCombo,
it will determine the target of that raise for you. This also works for Summoners and Red Mages.
