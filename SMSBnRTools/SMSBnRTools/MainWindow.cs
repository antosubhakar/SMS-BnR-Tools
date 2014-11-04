﻿using SMSBnRTools.classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace SMSBnRTools
{
    public partial class MainWindow : Form
    {
        private List<contact> contacts;
        private List<smsesSms> messages;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        #region file read

        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            this.Activate();
            string[] files = openFileDialog1.FileNames;

            if (files.Length != 1)
                return;

            string file = files[0];

            filePathInput.Text = file;

            ReadXml(file);

            Application.DoEvents();

        }


        private void ReadXml(string filename)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(smses));
            // A FileStream is needed to read the XML document.
            FileStream fs = new FileStream(filename, FileMode.Open);

            // Declare an object variable of the type to be deserialized.
            smses SMSObj;

            try{
                // Deserialize the object.
                SMSObj = (smses)serializer.Deserialize(fs);

                resultLabel.Text = "File read OK";

            }
            catch
            {
                resultLabel.Text = "Sorry, cannot read the file.";
                return;
            }
            fs.Close();

            contacts = SMSObj.Items.OfType<smsesSms>().GroupBy(x => new { x.address, x.contact_name })
                .Select(y => new contact()
                {
                    address = y.Key.address,
                    contact_name = y.Key.contact_name,
                    smses = y.ToList()
                }
                ).OrderBy(o => o.contact_name).ThenBy(p => p.address).ToList();
            contacts = ConsolidateContacts(contacts);
            contactsGV.DataSource = contacts;
        }

        /// <summary>
        /// merges messages from addresses with and without country code
        /// </summary>
        /// <param name="contax"></param>
        /// <returns></returns>
        private List<contact> ConsolidateContacts(List<contact> contax)
        {
            // use new list as collections cannot be modified within foreach
            List<contact> toConsolidate = contax.Where(x => x.address.StartsWith("0") && x.address.Length > 1).ToList();
            foreach (var c in toConsolidate)
            {
                if(contax.Any(x => !x.address.StartsWith("0") && x.address.EndsWith(c.address.Substring(1)))){
                    contact cMerge = contax.First(x => !x.address.StartsWith("0") && x.address.EndsWith(c.address.Substring(1)));
                    cMerge.smses.AddRange(c.smses);
                    cMerge.smses = cMerge.smses.OrderBy(o => o.date).ToList();
                    contax.Remove(c);
                }
            }
            return contax;
        }
        #endregion

        #region ui

        private void contactsGV_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (contactsGV.CurrentCell.RowIndex > -1 && contactsGV.CurrentCell.OwningRow.DataBoundItem != null)
            {
                ShowMessages((contact)contactsGV.CurrentCell.OwningRow.DataBoundItem);
                EnableButtons();
            }
            else
            {
                DisableButtons();
            }
        }

        private void ShowMessages(contact c)
        {
            messagesGV.DataSource = c.smses;
            messagesGV.Columns[2].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        }

        private void messagesGV_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex == 1 && e.Value != null)
            {
                switch ((byte)e.Value)
                {
                    case 1:
                        e.Value = "Received";
                        break;
                    case 2:
                        e.Value = "Sent";
                        break;
                    case 3:
                        e.Value = "Draft";
                        break;
                    case 4:
                        e.Value = "Outbox";
                        break;
                    case 5:
                        e.Value = "Failed";
                        break;
                    case 6:
                        e.Value = "Queued";
                        break;
                    default:
                        e.Value = "";
                        break;
                }
                e.FormattingApplied = true;
            }

        }

        #endregion

        #region buttons
        private void EnableButtons()
        {
            btnDelete.Enabled = true;
            if (contactsGV.SelectedRows.Count > 1)
                btnMerge.Enabled = true;
        }

        private void DisableButtons()
        {
            btnDelete.Enabled = false;
            btnMerge.Enabled = false;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (contactsGV.CurrentCell.RowIndex > -1 && contactsGV.CurrentCell.OwningRow.DataBoundItem != null)
            {
                var c = (contact)contactsGV.CurrentCell.OwningRow.DataBoundItem;
                //if (MessageBox.Show("Are you sure you want to delete the messages for this contact?\nNumber: " + c.address + "\nName: " + c.contact_name, "Delete Contact",
                //      MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                //      == DialogResult.Yes)
                //{
                    contacts.Remove(c);
                    contactsGV.EndEdit();
                    contactsGV.DataSource = null;
                    contactsGV.DataSource = contacts;
                    contactsGV.Refresh();
                    contactsGV.CurrentCell = null;
                    contactsGV.ClearSelection();
                    DisableButtons();
                //}
            }
        }

        private void btnMerge_Click(object sender, EventArgs e)
        {
            if (contactsGV.SelectedRows.Count > 1)
            {
                // first figure out which one is the latest contact (by newest message)
                int latestIndex = -1;
                ulong latestDate = 0;
                for (int i = 0; i < contactsGV.SelectedRows.Count; i++)
                {
                    contact c = (contact)contactsGV.SelectedRows[i].DataBoundItem;
                    if (c.smses.OrderBy(o => o.date).Last().date > latestDate)
                    {
                        latestIndex = i;
                        latestDate = c.smses.OrderBy(o => o.date).Last().date;
                    }
                }

                contact cMaster = (contact)contactsGV.SelectedRows[latestIndex].DataBoundItem;
                for (int i = 0; i < contactsGV.SelectedRows.Count; i++)
                {
                    if (i == latestIndex) // ignore the master contact
                        continue;
                    contact toMerge = (contact)contactsGV.SelectedRows[i].DataBoundItem;
                    cMaster.smses.AddRange(toMerge.smses);
                    // use the number that is associated with a contact_name
                    // TODO: select which contact to merge to?
                    if(cMaster.contact_name == contact.UNKNOWN_NAME && toMerge.contact_name != contact.UNKNOWN_NAME)
                    {
                        cMaster.contact_name = toMerge.contact_name;
                        cMaster.address = toMerge.address;
                    }
                    contacts.Remove(toMerge);
                }
                cMaster.smses = cMaster.smses.OrderBy(o => o.date).ToList();
                contactsGV.EndEdit();
                contactsGV.ClearSelection();
                contactsGV.CurrentCell = null;
                contactsGV.DataSource = null;
                contactsGV.DataSource = contacts;
                contactsGV.Refresh();
                DisableButtons();
            }
        }

        private void btnExportSelected_Click(object sender, EventArgs e)
        {
            if (contactsGV.CurrentCell.RowIndex > -1 && contactsGV.CurrentCell.OwningRow.DataBoundItem != null)
            {
                var c = (contact)contactsGV.CurrentCell.OwningRow.DataBoundItem;
                saveFileDialog1.FileName = c.contact_name != contact.UNKNOWN_NAME ? c.contact_name : (c.contact_name + " (" + c.address + ")");
                saveFileDialog1.ShowDialog();
            }
        }

        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            this.Activate();
            string[] files = saveFileDialog1.FileNames;

            if (files.Length != 1)
                return;

            string file = files[0];

            filePathInput.Text = file;

            if (contactsGV.CurrentCell.RowIndex > -1 && contactsGV.CurrentCell.OwningRow.DataBoundItem != null)
            {
                SaveXml(file, (contact)contactsGV.CurrentCell.OwningRow.DataBoundItem);
            }

            Application.DoEvents();
        }

        private void btnExportAll_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if(result == System.Windows.Forms.DialogResult.OK)
            {
                string folder = folderBrowserDialog1.SelectedPath;
                foreach(contact c in contacts)
                {
                    string filename = c.contact_name != contact.UNKNOWN_NAME ? c.contact_name : (c.contact_name + " (" + c.address + ")");
                    SaveXml(folder + "\\" + filename + ".xml", c);
                }
            }
        }

        private void SaveXml(string filename, contact c)
        {
            // TODO: handle existing files - check for duplicate messages, append new
            if (File.Exists(filename))
            {

            }

            XmlSerializer serializer = new XmlSerializer
                (typeof(smses), "http://www.cpandl.com");

            // Create an instance of the class to be serialized.
            smses s = new smses();
            s.Items = c.smses.ToArray();
            s.count = (ushort)s.Items.Count();

            using (XmlWriter w = XmlWriter.Create(filename))
            {
                w.WriteProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"sms.xsl\"");
                serializer.Serialize(w, s);
            }
            /*
            // Writing the document requires a TextWriter.
            TextWriter writer = new StreamWriter(filename);
            // Serialize the object, and close the TextWriter
            serializer.Serialize(writer, s);
            writer.Close();
            */
        }

        #endregion


    }
}
