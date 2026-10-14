import { createContext } from 'react';

/** True inside a read-only `EntityFormSheet`; shared fields render disabled. */
export const FormReadOnlyContext = createContext(false);
