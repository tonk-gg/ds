import ds from 'dawnseekers';

export default function update({ selected, world }) {

    const { tiles, seeker } = selected || {};
    const selectedTile = tiles && tiles.length === 1 ? tiles[0] : undefined;


    return {
        version: 1,
        components: [
            {
                type: 'building',
                id: 'prime-evil',
                title: 'Prime Evil',
                summary: "An invader from a totally different game. \n \"Welcome to Hell!\"",
                content: [
                    {
                        id: 'default',
                        type: 'inline',
                    },
                ],
            },
        ],
    };
}

