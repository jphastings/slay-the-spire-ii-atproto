import { lx } from 'prototypey';

export const sts2Run = lx.lexicon('me.byjp.pesos.sts2.run', {
	main: lx.record({
		description: 'A single Slay the Spire 2 run.',
		key: 'tid',
		record: lx.object({
			outcome: lx.string({
				required: true,
				description: 'How the run ended.',
				knownValues: ['in_progress', 'victory', 'death', 'abandoned']
			}),
			character: lx.string({
				required: true,
				description: "Character class id (e.g. 'ironclad', 'silent')."
			}),
			ascension: lx.integer({ required: true, minimum: 0, maximum: 20 }),
			seed: lx.string({
				required: true,
				description: 'Run seed as displayed in-game.'
			}),
			floor: lx.integer({ minimum: 0 }),
			act: lx.integer({ minimum: 1, maximum: 4 }),
			score: lx.integer({ minimum: 0 }),
			steamID64: lx.string({
				description: "SteamID64 of the run's owner, when playing on Steam."
			}),
			killedBy: lx.string({
				description: 'Enemy / event that ended the run, if applicable.'
			}),
			startedAt: lx.string({ required: true, format: 'datetime' }),
			endedAt: lx.string({ format: 'datetime' }),
			durationSeconds: lx.integer({ minimum: 0 }),
			deck: lx.array(lx.string(), { description: 'Card ids in the final deck.' }),
			relics: lx.array(lx.string(), { description: 'Relic ids held at run end.' }),
			potions: lx.array(lx.string(), { description: 'Potion ids currently held.' }),
			allies: lx.array(
				lx.object({
					steam: lx.string({ required: true, description: 'SteamID64 as a decimal string.' }),
					atproto: lx.string({ format: 'did', description: 'Atproto DID, when known.' })
				}),
				{ description: 'Other players in a multiplayer run.' }
			),
			game: lx.string({
				format: 'at-uri',
				description:
					'Optional at-uri of the canonical games.gamesgamesgamesgames.actor.game record for StS2.'
			}),
			statsRef: lx.string({
				format: 'at-uri',
				description:
					"at-uri of the user's games.gamesgamesgamesgames.actor.stats record for StS2, updated alongside this run."
			}),
			modVersion: lx.string(),
			gameVersion: lx.string(),
			updatedAt: lx.string({ required: true, format: 'datetime' })
		})
	})
});
