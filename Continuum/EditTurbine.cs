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
    public partial class EditTurbine : Form
    {
        public Continuum thisInst;

        public EditTurbine(Continuum continuum)
        {
            InitializeComponent();
            thisInst = continuum;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Edits the coordinates of selected turbine and calls background worker to perform turbine calcs (if they were done before)
                        
            string name = txtName.Text;
            double UTMX = Convert.ToDouble(txtUTMX.Text);
            double UTMY = Convert.ToDouble(txtUTMY.Text);

            if ( name == "" || UTMX == 0 || UTMY == 0 ) {
                MessageBox.Show("Need valid entries for all fields", "Continuum 2.3");
                return;
            }

            Check_class Check = new Check_class();
            bool inputTurbine = Check.NewTurbOrMet(thisInst, name, UTMX, UTMY, true);

            if (inputTurbine == true) {
                thisInst.turbineList.EditTurbine(name, UTMX, UTMY);

                if (thisInst.turbineList.turbineCalcsDone == true) {
                    BackgroundWork.Vars_for_Turbine_and_Node_Calcs argsForBW = new BackgroundWork.Vars_for_Turbine_and_Node_Calcs();
                    
                    if (thisInst.metList.ThisCount > 0 && thisInst.turbineList.turbineCalcsDone == true)
                    {
                        argsForBW.thisInst = thisInst;
                        argsForBW.thisWakeModel = null;
                        argsForBW.isCalibrated = true;

                        // Call background worker to run calculations
                        thisInst.BW_worker = new BackgroundWork();
                        thisInst.BW_worker.Call_BW_TurbCalcs(argsForBW);
                    }
                }
            }

            Update updateThe = new Update();
            updateThe.TurbineList(thisInst);
            Close();
        }
        
        private void btnCancel_Click(object sender, EventArgs e)
        {
            // Closes form
            Close();
        }
                
    }
}