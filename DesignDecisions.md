# Design Decisions
This is a small markdown writeup to explain some of the design decisions I've made for this test project.

## Validation
- Opted to keep this simple, operating under the assumption that any errors returned will be part of a bad request response.
- Chose a non generic solution as currently only one request type is being validated, for a more complex project I would add a generic interface and implement it for each request type.
- Ignoring Luhn check digit algorithm for card number validation, but might be worth considering in a real scenario.
- Using my own validation logic instead of a library like FluentValidation to avoid adding dependencies, but would use something like that in a real scenario for readable logic and nice error messages
- Rejecting any storage request for PostPaymentResponse that have an id that's already in the repository to guard against duplicate requests

## Modelling
- Changed the type of CardNumberLastFour and CVV from an int to a string as no mathematical operations are performed on it. May also lead to issues with numbers like 0024 dropping prefix zeros.
- Marked strings as required as assuming that null values are unacceptable.
- Using full card number and cvv in PostPaymentRequest, not storing them but am transmitting them so tokenization should be used in a real system.
- Removed rejection from PaymentStatus as rejected requests aren't stored, malformatted requests can't be used for payment
- Assuming that authorization code is something useful and should be stored if it's returned, using PostPaymentResponse for this.
- Using GetPaymentResponse to return stored data for PostPaymentResponses but stripping out the authorization code as that might be sensitive.

## Storage
- Roughly keeping to provided mimic repository for storage of PostPaymentRequest storage as proper storage via DB is out of scope

## Use of Interfaces
- Not strictly necessary here but adding for more flexibility in test mocking and to keep business logic and concrete implemntations separate.

## Currency handling
- Kept currency types as basic strings as the distinction doesn't matter too much for this task, but a currency object containing the following would be a good idea in a larger project: 
    - ISO 4217 currency code, e.g. GBP 
    - Unicode symbol
    - Subunit size, usually 2 but can be different, e.g. Japanese Yen with 0 or Kuwaiti Dinar with 3
- In a bigger project, supported currencies should probably be a configuration item as they may change over time

## Controllers
- Switched inheritance from Controller to ControllerBase as we don't need views.


