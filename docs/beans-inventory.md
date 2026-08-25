# Beans inventory and detail

The Beans surface is a responsive inventory browser built on the shared Direction B tokens and calm,
neutral browsing cards.

## Inventory behavior

- Search is an in-memory, cancelable projection over name, country, variety, process, and notes.
- Filters separate available, low (250 g or less), and out-of-stock beans without reordering stored data.
- Reads retain the last visible rows on failure and expose an inline Retry action.
- Add and Start Roast are amber actions; Low is yellow; out-of-stock remains neutral and explicit.
- Below 600 dp, tapping a bean pushes its detail page. At 600 dp and above, the selected bean opens in
  a list/detail pane and the full detail route remains available.

## Identity and roast linkage

Bean detail queries roast history through `IRoastQueryService` by `BeanData.Id`. It shows the newest
`Complete` roast as the reference result and lists recent `AwaitingWeight` or `Unweighed` work
separately. Renaming a bean never rewrites `BeanDisplaySnapshot` on historical roasts.

Start Roast navigates with the stable `BeanId`. The receiving roast setup performs the final Ticket 02
carry-forward lookup and keeps the bean, temperature, and batch weight as a confirmation state; it does
not skip directly into an active roast.
