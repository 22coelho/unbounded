using System;
using System.Collections.Generic;

namespace Unbounded.Race
{
    /// <summary>
    /// Orders a Race field. Cars are ranked by Laps completed, then Checkpoints
    /// passed on the current Lap, then distance along the Track. Cars with
    /// identical progress keep their field order, so the result is stable from
    /// frame to frame instead of flickering between tied cars.
    /// </summary>
    public static class Standings
    {
        /// <summary>
        /// The field indices in race order: the first entry is the leader. The
        /// order is recomputed from live distance, so places change as cars pass
        /// each other on the Track rather than only at Checkpoints.
        /// </summary>
        public static int[] Order(IReadOnlyList<RacerProgress> field)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            var order = new int[field.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (left, right) =>
            {
                int byProgress = field[right].CompareTo(field[left]);
                return byProgress != 0 ? byProgress : left.CompareTo(right);
            });

            return order;
        }

        /// <summary>
        /// The one-based position of the car at <paramref name="index"/>, or
        /// zero when that index is not in the field. Exact ties are broken by
        /// field order, matching <see cref="Order"/>.
        /// </summary>
        public static int PositionOf(IReadOnlyList<RacerProgress> field, int index)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (index < 0 || index >= field.Count)
            {
                return 0;
            }

            int position = 1;
            for (int i = 0; i < field.Count; i++)
            {
                if (i == index)
                {
                    continue;
                }

                int ahead = field[i].CompareTo(field[index]);
                if (ahead > 0 || (ahead == 0 && i < index))
                {
                    position++;
                }
            }

            return position;
        }
    }
}
