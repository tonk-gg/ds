import { map, pipe, Source, switchMap } from 'wonka';
import { SelectedPlayerFragment } from './gql/graphql';

/**
 * makePlayerMobileUnit checks if the provided mobileUnit id exists on the current player
 * before either voiding the selection or selecting that mobileUnit
 *
 * if no mobileUnit selection is requested then we default to the first mobileUnit.
 */
export function makePlayerMobileUnit(
    player: Source<SelectedPlayerFragment | undefined>,
    id: Source<string | undefined>,
) {
    return pipe(
        player,
        switchMap((player) =>
            pipe(
                id,
                map((id) => {
                    const mobileUnit = player ? player.mobileUnits.find((s) => s.id === id) : undefined;
                    return mobileUnit;
                }),
            ),
        ),
    );
}
