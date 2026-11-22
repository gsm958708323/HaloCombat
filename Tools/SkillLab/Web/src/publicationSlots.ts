export const publicationSlots = ['f6', 'f7', 'slot3', 'slot4'] as const;
export type PublicationSlot = typeof publicationSlots[number];
export const publicationSlotLabel = (slot: PublicationSlot) => `槽位 ${publicationSlots.indexOf(slot) + 1}`;
