﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ContinuumNS
{
    public partial class NewTurbine : Form
    {
        Continuum thisInst;

        public NewTurbine(Continuum continuum)
        {
            InitializeComponent();
            thisInst = continuum;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Reads in entered name, UTMX, UTMY, and string number. if ( valid, adds to turbine list and calls background worker to perform turbine calcs (if done before)
            string name = "";
            double UTMX = 0;
            double UTMY = 0;
            int stringNum = 0;

            bool inputTurbine = false;
            Check_class check = new Check_class();

            try
            {
                name = txtName.Text;
            }
            catch 
            {
                MessageBox.Show("Invalid entry for turbine name", "Continuum 2.3");
                return;
            }

            try
            {
                UTMX = Convert.ToSingle(txtUTMX.Text);
            }
            catch 
            {
                MessageBox.Show("Invalid entry for easting", "Continuum 2.3");
                return;
            }

            try
            {
                UTMY = Convert.ToSingle(txtUTMY.Text);
            }
            catch
            {
                MessageBox.Show("Invalid entry for northing", "Continuum 2.3");
                return;
            }

            try
            {
                stringNum = Convert.ToInt16(txtStrNum.Text);
            }
            catch 
            {
                stringNum = 0;
            }

            if (name == "" || UTMX == 0 || UTMY == 0)
            {
                MessageBox.Show("Need valid entries for all fields", "Continuum 2.3");
                return;
            }
            else
            {

                inputTurbine = check.NewTurbOrMet(thisInst, name, UTMX, UTMY, true);
                if (inputTurbine == true) thisInst.turbineList.AddTurbine(name, UTMX, UTMY, stringNum);
                Update UpdateThis = new Update();

                if (thisInst.turbineList.turbineCalcsDone == true)
                {
                    BackgroundWork.Vars_for_Turbine_and_Node_Calcs argsForBW = new BackgroundWork.Vars_for_Turbine_and_Node_Calcs();
                    

                    if (thisInst.metList.ThisCount > 0)
                    {
                        argsForBW.thisInst = thisInst;
                        argsForBW.thisWakeModel = null;
                        argsForBW.isCalibrated = false;

                        // Call background worker to run calculations
                        thisInst.BW_worker = new BackgroundWork();
                        thisInst.BW_worker.Call_BW_TurbCalcs(argsForBW);
                    }
                    else
                    {
                        UpdateThis.TurbineList(thisInst);
                        thisInst.ChangesMade();
                    }                   

                }
                else
                {                    
                    UpdateThis.TurbineList(thisInst);
                    thisInst.ChangesMade();
                }
                Close();
            }
        }

        
    }
}
