using Stripe;
using System;
using System.Collections.Generic;
using System.Text;

{
    public class StripeService : IStripeService
    {
        public Account CreateConnectAccount(string accountType, string country, string interval)
        {
            var options = new AccountCreateOptions
            {
                Type = accountType,
                Country = country,

                Capabilities = new AccountCapabilitiesOptions
                {
                    CardPayments = new AccountCapabilitiesCardPaymentsOptions
                    {
                        Requested = true,
                    },
                    Transfers = new AccountCapabilitiesTransfersOptions
                    {
                        Requested = true,
                    },
                },
                Settings = new AccountSettingsOptions
                {
                    Payouts = new AccountSettingsPayoutsOptions
                    {
                        Schedule = new AccountSettingsPayoutsScheduleOptions
                        {
                            Interval = interval,
                        },
                        DebitNegativeBalances = true,
                    },
                }
            };
            var service = new Stripe.AccountService();
            var response = service.Create(options);
            return response;
        }


        public AccountLink StripeConnectOnboarding(string connectAccountId, string refresUrl, string returnUrl, string type, string collect)
        {
            var options = new AccountLinkCreateOptions
            {
                Account = connectAccountId,
                RefreshUrl = refresUrl,
                ReturnUrl = returnUrl,
                Type = type,
                Collect = collect,

            };
            var service = new AccountLinkService();
            var accountLink = service.Create(options);
            return accountLink;
        }

        public void AddNewCardToConnectAccount(string token, string connectAccountId)
        {
            var options = new ExternalAccountCreateOptions
            {
                ExternalAccount = token,
            };
            var service = new ExternalAccountService();
            var reponse = service.Create(connectAccountId, options);
        }


        public PaymentIntent ChargeCustomerDefaultPaymentMethod(string customerId, long? amount, string destinationAccountId, string ISOCurrencyCode, long? serviceFeeAmount, int invoiceId)
        {
            var createOptions = new PaymentIntentCreateOptions
            {

                Amount = amount,
                Currency = ISOCurrencyCode,
                Confirm = true,
                Customer = customerId,
                OffSession = true,
                OnBehalfOf = destinationAccountId,
                TransferData = new PaymentIntentTransferDataOptions
                {
                    Destination = destinationAccountId,
                    Amount = (long)(amount - serviceFeeAmount)
                },
                Metadata = new Dictionary<string, string>
                    {
                        { "InvoiceId", invoiceId.ToString() },
                    }
            };
            var service = new PaymentIntentService();
            var response = service.Create(createOptions);


            return response;
        }

        public PaymentIntent ChargeCustomerSpecificPaymentMethod(string customerId, string savedCardId, long? amount, string destinationAccountId, string ISOCurrencyCode, long? serviceFeeAmount, int invoiceId)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                PaymentMethod = savedCardId,
                Amount = amount,
                Currency = ISOCurrencyCode,
                Confirm = true,
                Customer = customerId,
                OffSession = true,
                OnBehalfOf = destinationAccountId,

                TransferData = new PaymentIntentTransferDataOptions
                {
                    Destination = destinationAccountId,
                    Amount = (long)(amount - serviceFeeAmount)

                },
                Metadata = new Dictionary<string, string>
                    {
                        { "InvoiceId", invoiceId.ToString() },
                    }
            };
            var service = new PaymentIntentService();
            var response = service.Create(createOptions);


            return response;
        }


        public Card AddNewCardToExistingCustomer(string tokenId, string customerId)
        {
            var options = new CardCreateOptions
            {
                Source = tokenId,

            };
            var service = new CardService();
            var response = service.Create(customerId, options);


            var options1 = new SetupIntentCreateOptions
            {
                PaymentMethodTypes = new List<string>
              {
                "card",
              },
                Customer = response.CustomerId,
                PaymentMethod = response.Id,
                Confirm = true,
            };
            var service1 = new SetupIntentService();
            service1.Create(options1);



            var options2 = new SetupIntentListOptions
            {

                Customer = response.CustomerId,

            };
            var service2 = new SetupIntentService();
            StripeList<SetupIntent> setupIntents2 = service2.List(
              options2
            );




            return response;
        }

        public Customer CreateNewCustomer(string cardHolderName, string email, string tokenId)
        {
            var options = new CustomerCreateOptions
            {
                Name = cardHolderName,
                Email = email,
                Source = tokenId,
            };
            var service = new CustomerService();
            var response = service.Create(options);


            var options1 = new SetupIntentCreateOptions
            {
                PaymentMethodTypes = new List<string>
              {
                "card",
              },
                Customer = response.Id,
                PaymentMethod = response.DefaultSourceId,
                Confirm = true,
            };
            var service1 = new SetupIntentService();
            service1.Create(options1);

            return response;
        }

        public Payout TransferFundFromStripeToBankAccount(long? amount, string currency, string connectAccountId, int invoiceId)
        {
            var options = new PayoutCreateOptions
            {
                Amount = amount,
                Currency = currency,
                Metadata = new Dictionary<string, string>
                    {
                        { "InvoiceId", invoiceId.ToString() },
                    }
            };

            var requestOptions = new RequestOptions();
            requestOptions.StripeAccount = connectAccountId;
            var service = new PayoutService();
            var payout = service.Create(options, requestOptions);
            return payout;
        }

        public Balance GetStripeConnectAccountBalance(string connectAccountId)
        {
            var requestOptions = new RequestOptions();
            requestOptions.StripeAccount = connectAccountId;
            var service = new BalanceService();
            var balance = service.Get(requestOptions);
            return balance;
        }


        public Card GetCardByCustomerIdCardId(string customerId, string cardId)
        {
            var service = new CardService();
            return service.Get(
                                 customerId,
                                 cardId
                            );
        }

        public Customer ChangeCustomerDefaultPaymentMethod(string cardId, string customerId)
        {
            var options = new CustomerUpdateOptions
            {
                DefaultSource = cardId,

            };
            var service = new CustomerService();
            return service.Update(customerId, options);
        }

        public PaymentIntent ChargeCustomerSubscriptionWithDefaultPaymentMethod(string customerId, long? amount, string ISOCurrencyCode)
        {
            var createOptions = new PaymentIntentCreateOptions
            {

                Amount = amount,
                Currency = ISOCurrencyCode,
                Customer = customerId,
                Confirm = true,
                OffSession = true,

            };
            var service = new PaymentIntentService();
            var response = service.Create(createOptions);
            return response;
        }

        public PaymentIntent ChargeCustomerSubscriptionSpecificPaymentMethod(string customerId, string savedCardId, long? amount, string ISOCurrencyCode, int subscriptionInvoiceId)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                PaymentMethod = savedCardId,
                Amount = amount,
                Currency = ISOCurrencyCode,
                Confirm = true,
                Customer = customerId,
                OffSession = true,
                Metadata = new Dictionary<string, string>
                    {
                        { "SubscriptionInvoiceId", subscriptionInvoiceId.ToString() },
                    }          
            };
            var service = new PaymentIntentService();
            var response = service.Create(createOptions);


            return response;
        }
        public PaymentIntent ChargeCustomerJobSecuritySpecificPaymentMethod(string customerId, string savedCardId, long? amount, string ISOCurrencyCode, int securityChargeInvoiceId)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                PaymentMethod = savedCardId,
                Amount = amount,
                Currency = ISOCurrencyCode,
                Confirm = true,
                Customer = customerId,
                OffSession = true,
                Metadata = new Dictionary<string, string>
                    {
                        { "SecurityChargeInvoiceId", securityChargeInvoiceId.ToString() },
                    }               
            };
            var service = new PaymentIntentService();
            var response = service.Create(createOptions);


            return response;
        }


        public PaymentIntent ChargeCustomerAddOnPlanPaymentMethod(string customerId, string savedCardId, long? amount, string ISOCurrencyCode, int addOnPlanInvoiceId)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                PaymentMethod = savedCardId,
                Amount = amount,
                Currency = ISOCurrencyCode,
                Confirm = true,
                Customer = customerId,
                OffSession = true,
                Metadata = new Dictionary<string, string>
                    {
                        { "AddOnPlanInvoiceId", addOnPlanInvoiceId.ToString() },
                    }               
            };
            var service = new PaymentIntentService();
            var response = service.Create(createOptions);


            return response;
        }

        public Refund RefundPaymentByPaymentIntent(string paymentIntent)
        {
            var refunds = new RefundService();
            var refundOptions = new RefundCreateOptions
            {
                PaymentIntent = paymentIntent,
                RefundApplicationFee = true,
            };
            var refund = refunds.Create(refundOptions);

            return refund;
        }

        public Refund RefundPaymentByPaymentIntentWithoutApplicationFee(string paymentIntent)
        {
            var refunds = new RefundService();
            var refundOptions = new RefundCreateOptions
            {
                PaymentIntent = paymentIntent,
                RefundApplicationFee = true,
                ReverseTransfer = true
            };
            var refund = refunds.Create(refundOptions);

            return refund;
        }

        public PaymentIntent ChargeCustomerWithDefaultPaymentMethodForSubscription(string customerId, long? amount, string ISOCurrencyCode)
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = ISOCurrencyCode,
                Customer = customerId,
                Confirm = true,
                OffSession = true,
            };
            var service = new PaymentIntentService();
            var response = service.Create(createOptions);
            return response;
        }

        public PaymentIntent GetPaymentIntentByPaymentIntentId(string paymentIntent)
        {
            var service = new PaymentIntentService();
            return service.Get(paymentIntent);
        }

        public Transfer GetTransferByTransferId(string transferId)
        {
            var service = new TransferService();
            return service.Get(transferId);
        }
        public BalanceTransaction GetBalanceTransactionById(string balanceTransactionId)
        {

            var service = new BalanceTransactionService();
            return service.Get(balanceTransactionId);

        }
        public BalanceTransaction GetPlatformBalanceTransactionById(string balanceTransactionId, string destinationId)
        {
            var requestOptions = new RequestOptions();
            requestOptions.StripeAccount = destinationId;
            var service = new BalanceTransactionService();
            return service.Get(balanceTransactionId, null, requestOptions);

        }
        public Charge GetChargeByChargeId(string chargeId)
        {
            var service = new ChargeService();
            return service.Get(chargeId);

        }
        public Charge GetPlatformChargeByChargeId(string chargeId, string destinationId)
        {
            var requestOptions = new RequestOptions();
            requestOptions.StripeAccount = destinationId;
            var service = new ChargeService();
            return service.Get(chargeId, null, requestOptions);

        }

        public ApplicationFee GetApplicationFeeById(string applicationFeeId)
        {
            var service = new ApplicationFeeService();
            return service.Get(applicationFeeId);
        }
        public Payout GetPayoutByPayoutId(string payoutId, string connectAccountId)
        {
            var requestOptions = new RequestOptions();
            requestOptions.StripeAccount = connectAccountId;
            var service = new PayoutService();
            return service.Get(payoutId, null, requestOptions);
        }

        public BalanceTransaction GetPayoutBalanceTransactionByTransactionId(string balanceTransactionId, string connectAccountId)
        {
            var requestOptions = new RequestOptions();
            requestOptions.StripeAccount = connectAccountId;
            var service = new BalanceTransactionService();
            return service.Get(balanceTransactionId, null, requestOptions);

        }

        public Refund GetRefundByRefundId(string refundId)
        {
            var service = new RefundService();
            return service.Get(refundId);
        }

        public TransferReversal GetTransferReversal(string transferId, string reveretransferId)
        {
            var service = new TransferReversalService();
            return service.Get(transferId, reveretransferId);
        }

    }
}
