using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Beanstream.Api.SDK;
using Beanstream.Api.SDK.Domain;
using Beanstream.Api.SDK.Requests;
using Beanstream.Api.SDK.Data;
using Beanstream.Api.SDK.Exceptions;

using System.IO;

namespace ManageCreditCard
{
    /// <summary>
    /// Credit Card Payments
    /// An application that uses Beantream as payment gateway and stores succesful transactions in a database.
    /// It also saves those transaction into csv file a a backup.
    /// </summary>
    public partial class CreditCardPayment : Form
    {
        // Constant varibales
        const string paymentsAPIKeyString = "API_KEY";
        const int merchantIDInt = 123456789;
        const string APIVersionInt = "1";
        //Constant messages srings
        const string validationInvalidString = "Incomplete/Invalid Information";
        const string validationApprovedString = "Approved";
        const string validationDeclinedString = "Declined";
        const string validationIncompleteString = "Please provide information for field: ";

        public CreditCardPayment()
        {
            InitializeComponent();
        }

        private void CreditCardPayment_Load(object sender, EventArgs e)
        {
            // TODO: This line of code loads data into the 'paymentsDataSet.Payments' table.
            this.paymentsTableAdapter.Fill(this.paymentsDataSet.Payments);

        }

        private void paymentsBindingNavigatorSaveItem_Click(object sender, EventArgs e)
        {
            this.Validate();
            this.paymentsBindingSource.EndEdit();
            this.tableAdapterManager.UpdateAll(this.paymentsDataSet);

        }
        /// <summary>
        /// Process payment
        /// </summary>
        private void submitButton_Click(object sender, EventArgs e)
        {
            try
            {
                //Perform validation to male sure required fields were filled. Throw ArgumentException with custom message if not
                if(amountTextBox.Text == String.Empty)
                {
                    throw new System.ArgumentException(validationIncompleteString + amountLabel.Text);
                }
                if (Convert.ToDouble(amountTextBox.Text) <= 0)
                {
                    throw new System.ArgumentException("Please enter a valid amount. Must be greater the 0.");
                }
                if (nameTextBox.Text == String.Empty)
                {
                    throw new System.ArgumentException(validationIncompleteString + nameLabel.Text);
                }
                if (cardNumberTextBox.Text == String.Empty)
                {
                    throw new System.ArgumentException(validationIncompleteString + cardNumberLabel.Text);
                }
                if (monthComboBox.SelectedIndex == -1)
                {
                    throw new System.ArgumentException(validationIncompleteString + monthLabel.Text);
                }
                if (yearComboBox.SelectedIndex == -1)
                {
                    throw new System.ArgumentException(validationIncompleteString + yearLabel.Text);
                }
                // Initialize Gateway connection
                Gateway bsGateway = new Gateway();
                bsGateway.MerchantId = merchantIDInt;
                bsGateway.PaymentsApiKey = paymentsAPIKeyString;
                bsGateway.ApiVersion = APIVersionInt;

                // Setup the Credit Card details
                Card ccCard = new Card();
                ccCard.Name = nameTextBox.Text;
                ccCard.Number = cardNumberTextBox.Text;
                ccCard.ExpiryMonth = monthComboBox.Items[monthComboBox.SelectedIndex].ToString();
                ccCard.ExpiryYear = yearComboBox.Items[yearComboBox.SelectedIndex].ToString(); 
                ccCard.Cvd = ccvMaskedTextBox.Text;
                //Capture time when the payment was processed
                DateTime now = DateTime.Now;
                // Setup the payment request
                CardPaymentRequest reqCardPaymentRequest = new CardPaymentRequest();
                reqCardPaymentRequest.Amount = Convert.ToDouble(amountTextBox.Text);
                reqCardPaymentRequest.OrderNumber = GetTimestamp(now); // Use current to create a unique order, call method that generates it
                reqCardPaymentRequest.Card = ccCard;

                // Process the payment and get the response from their servers            
                PaymentResponse response = bsGateway.Payments.MakePayment(reqCardPaymentRequest);//trace the value, debug f9                
                if (response.Message == validationApprovedString)
                {
                    paymentsTableAdapter.Insert(
                        response.Created.Date.ToString(),
                        response.Message.ToString(),
                        response.Card.CardType.ToString(),
                        nameTextBox.Text,
                        response.Card.LastFour.ToString(),
                        Convert.ToDecimal(amountTextBox.Text),
                        response.OrderNumber.ToString(),
                        response.TransactionId.ToString());
                    paymentsTableAdapter.Fill(paymentsDataSet.Payments);
                }
                MessageBox.Show("Your payment was processed successfully!","Payment" + validationApprovedString, MessageBoxButtons.OK, MessageBoxIcon.Information);
                //Clear all fields
                amountTextBox.Text = String.Empty;
                nameTextBox.Text = String.Empty;
                cardNumberTextBox.Text = String.Empty;
                monthComboBox.SelectedIndex = -1;
                yearComboBox.SelectedIndex = -1;
                ccvMaskedTextBox.Text = String.Empty;
            }
            catch(InvalidRequestException)
            {
                MessageBox.Show("Credit Card Declined.\nUnable to process payment.","Payment" + validationDeclinedString, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch(ArgumentException argumentException)
            {
                MessageBox.Show(argumentException.Message.ToString(), validationInvalidString, MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
            catch (FormatException)
            {
                MessageBox.Show("You must enter a number in the amount field.", validationInvalidString, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to process payment.\nPlease check that required information is complete and correct", "Process Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Generate a unique order based on the DateTime value passed
        /// </summary>
        /// <param name="currentTime"></param>
        /// <returns>Formatted string as yyyyMMddHHmmssfff</returns>
        public String GetTimestamp(DateTime currentTime)
        {
            return currentTime.ToString("yyyyMMddHHmmssfff");
        }
        /// <summary>
        /// Exit the application through the exit option menu
        /// </summary>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Generate a backup of the database records as a csv file. Give user option to name and select location of the file.
        /// </summary>
        private void backupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fileNameString;
            //string[] recordsString = new string[100];
            List<String> recordsList = new List<String>();

            DialogResult drDialogResult; //Store the dialog result
            backupSaveFileDialog.Filter = "CSV Files |*csv";
            backupSaveFileDialog.FileName = "payments.csv";
            backupSaveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            backupSaveFileDialog.OverwritePrompt = true;
            drDialogResult = backupSaveFileDialog.ShowDialog();
            if (drDialogResult != DialogResult.Cancel)
            {
                //Show in text box selected path and default video name
                fileNameString = backupSaveFileDialog.FileName;
                //Loop to save DB records in array to make backup
                foreach(PaymentsDataSet.PaymentsRow record in paymentsDataSet.Payments.Rows)
                {
                    recordsList.Add(record.PaymentDate + "," +
                                                record.PaymentStatus + "," +
                                                record.NameOnCard + "," +
                                                record.LastFourDigits + "," +
                                                record.PaymentAmount + "," +
                                                record.OrderNumber + "," +
                                                record.TransactionID);
                }
                string[] recordsString = recordsList.ToArray();
                try
                {
                    //Save backup of DB to csv file
                    File.WriteAllLines(fileNameString, recordsString);
                    MessageBox.Show("Your backup of the database was successfully saved.","Backup Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception)
                {
                    MessageBox.Show("There was an error when saving the backup of the database.\nPlease try again.","Error Saving Backup",MessageBoxButtons.OK,MessageBoxIcon.Information);
                }

            }
        }
        /// <summary>
        /// Clear all the input fields
        /// </summary>
        private void clearFieldsButton_Click(object sender, EventArgs e)
        {
            amountTextBox.Text = String.Empty;
            nameTextBox.Text = String.Empty;
            cardNumberTextBox.Text = String.Empty;
            monthComboBox.SelectedIndex = -1;
            yearComboBox.SelectedIndex = -1;
            ccvMaskedTextBox.Text = String.Empty;
        }
        /// <summary>
        /// Restrict user from writing not numeric values in amount field, with exception of backspace and point (since can be decimal)
        /// </summary>
        private void amountTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!Char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && e.KeyChar != '.')
            {
                e.Handled = true;
            }
        }
        /// <summary>
        /// Restrict user from writing not numeric values in credit card field, with exception of backspace
        /// </summary>
        private void cardNumberTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!Char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
            }
        }
    }
}
