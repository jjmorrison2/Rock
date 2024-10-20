﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.Finance
{
    /// <summary>
    /// Block for users to create, edit, and view benevolence requests.
    /// </summary>
    [DisplayName( "Benevolence Request Detail" )]
    [Category( "Finance" )]
    [Description( "Block for users to create, edit, and view benevolence requests." )]

    [SecurityRoleField( "Case Worker Role",
        Description = "The security role to draw case workers from",
        IsRequired = false,
        Key = AttributeKey.CaseWorkerRole,
        Order = 0 )]

    [LinkedPage( "Benevolence Request Statement Page",
        Description = "The page which summarizes a benevolence request for printing",
        IsRequired = true,
        Key = AttributeKey.BenevolenceRequestStatementPage,
        Order = 1 )]
    public partial class BenevolenceRequestDetail : Rock.Web.UI.RockBlock
    {
        #region Attribute Keys

        private static class AttributeKey
        {
            public const string CaseWorkerRole = "CaseWorkerRole";
            public const string BenevolenceRequestStatementPage = "BenevolenceRequestStatementPage";
        }

        #endregion Attribute Keys

        #region PageParameterKeys

        private static class PageParameterKey
        {
            public const string BenevolenceRequestId = "BenevolenceRequestId";
        }

        #endregion PageParameterKeys

        #region Fields 

        private Guid? _caseWorkerGroupGuid = null;
        private BenevolenceResult _mocBenevolenceResult;

        #endregion

        #region Properties

        private List<int> DocumentsState { get; set; }

        private BenevolenceResult MockBenevolenceResult
        {
            get
            {
                if ( _mocBenevolenceResult == null )
                {
                    _mocBenevolenceResult = new BenevolenceResult();
                    _mocBenevolenceResult.LoadAttributes();
                }
                return _mocBenevolenceResult;
            }
        }

        #endregion

        #region ViewState and Dynamic Controls

        /// <summary>
        /// ViewState of BenevolenceResultInfos for BenevolenceRequest
        /// </summary>
        /// <value>
        /// The state of the BenevolenceResultInfos for BenevolenceRequest.
        /// </value>
        public List<BenevolenceResultInfo> BenevolenceResultsState
        {
            get
            {
                List<BenevolenceResultInfo> result = ViewState["BenevolenceResultInfoState"] as List<BenevolenceResultInfo>;
                if ( result == null )
                {
                    result = new List<BenevolenceResultInfo>();
                }

                return result;
            }

            set
            {
                ViewState["BenevolenceResultInfoState"] = value;
            }
        }

        /// <summary>
        /// Adds columns to the results grid 
        /// </summary>
        private void AddDynamicColumns()
        {
            var attributes = MockBenevolenceResult.Attributes.Select( a => a.Value ).Where( a => a.IsGridColumn ).ToList();

            foreach ( var attribute in attributes )
            {
                bool columnExists = gResults.Columns.OfType<AttributeField>().FirstOrDefault( a => a.AttributeId == attribute.Id ) != null;
                if ( !columnExists )
                {
                    AttributeField boundField = new AttributeField();
                    boundField.DataField = attribute.Key;
                    boundField.AttributeId = attribute.Id;
                    boundField.HeaderText = attribute.Name;

                    var attributeCache = Rock.Web.Cache.AttributeCache.Get( attribute.Id );
                    if ( attributeCache != null )
                    {
                        boundField.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                    }

                    gResults.Columns.Add( boundField );
                }
            }

            // Add delete column
            var deleteField = new DeleteField();
            gResults.Columns.Add( deleteField );
            deleteField.Click += gResults_Delete;
        }

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
            gResults.DataKeyNames = new string[] { "TempGuid" };
            gResults.Actions.AddClick += gResults_AddClick;
            gResults.Actions.ShowAdd = true;
            gResults.IsDeleteEnabled = true;

            // Gets any existing results and places them into the ViewState
            BenevolenceRequest benevolenceRequest = null;
            int benevolenceRequestId = PageParameter( PageParameterKey.BenevolenceRequestId ).AsInteger();
            if ( !benevolenceRequestId.Equals( 0 ) )
            {
                benevolenceRequest = new BenevolenceRequestService( new RockContext() ).Get( benevolenceRequestId );
            }

            if ( benevolenceRequest == null )
            {
                benevolenceRequest = new BenevolenceRequest { Id = 0 };
            }

            if ( ViewState["BenevolenceResultInfoState"] == null )
            {
                List<BenevolenceResultInfo> brInfoList = new List<BenevolenceResultInfo>();
                foreach ( BenevolenceResult benevolenceResult in benevolenceRequest.BenevolenceResults )
                {
                    BenevolenceResultInfo benevolenceResultInfo = new BenevolenceResultInfo();
                    benevolenceResultInfo.ResultId = benevolenceResult.Id;
                    benevolenceResultInfo.Amount = benevolenceResult.Amount;
                    benevolenceResultInfo.TempGuid = benevolenceResult.Guid;
                    benevolenceResultInfo.ResultSummary = benevolenceResult.ResultSummary;
                    benevolenceResultInfo.ResultTypeValueId = benevolenceResult.ResultTypeValueId;
                    benevolenceResultInfo.ResultTypeName = benevolenceResult.ResultTypeValue.Value;
                    benevolenceResult.LoadAttributes();
                    benevolenceResultInfo.Attributes = benevolenceResult.Attributes;
                    benevolenceResultInfo.AttributeValues = benevolenceResult.AttributeValues;
                    brInfoList.Add( benevolenceResultInfo );
                }

                BenevolenceResultsState = brInfoList;
            }

            dlDocuments.ItemDataBound += DlDocuments_ItemDataBound;

            _caseWorkerGroupGuid = GetAttributeValue( AttributeKey.CaseWorkerRole ).AsGuidOrNull();

            AddDynamicColumns();
        }


        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                cpCampus.Campuses = CampusCache.All();
                ShowDetail( PageParameter( PageParameterKey.BenevolenceRequestId ).AsInteger() );
            }
            else
            {
                var rockContext = new RockContext();
                BenevolenceRequest item = new BenevolenceRequestService( rockContext ).Get( hfBenevolenceRequestId.ValueAsInt() );
                if ( item == null )
                {
                    item = new BenevolenceRequest();
                }
                item.LoadAttributes();

                phAttributes.Controls.Clear();
                Rock.Attribute.Helper.AddEditControls( item, phAttributes, false, BlockValidationGroup, 2 );


                Rock.Attribute.Helper.AddEditControls( MockBenevolenceResult, phResultAttributes, false, valResultsSummary.ValidationGroup, 2 );

                confirmExit.Enabled = true;
            }
        }


        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            DocumentsState = ViewState["DocumentsState"] as List<int>;
            if ( DocumentsState == null )
            {
                DocumentsState = new List<int>();
            }
        }

        /// <summary>
        /// Saves any user control view-state changes that have occurred since the last page postback.
        /// </summary>
        /// <returns>
        /// Returns the user control's current view state. If there is no view state associated with the control, it returns null.
        /// </returns>
        protected override object SaveViewState()
        {
            ViewState["DocumentsState"] = DocumentsState;

            return base.SaveViewState();
        }
        #endregion

        #region Events

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            ShowDetail( PageParameter( PageParameterKey.BenevolenceRequestId ).AsInteger() );
        }

        /// <summary>
        /// Handles the AddClick event of the gResults control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        protected void gResults_AddClick( object sender, EventArgs e )
        {
            dvpResultType.Items.Clear();
            dvpResultType.AutoPostBack = false;
            dvpResultType.Required = true;
            dvpResultType.DefinedTypeId = DefinedTypeCache.Get( new Guid( Rock.SystemGuid.DefinedType.BENEVOLENCE_RESULT_TYPE ) ).Id;
            dtbResultSummary.Text = string.Empty;
            dtbAmount.Value = null;
            hfInfoGuid.Value = Guid.NewGuid().ToString();

            phResultAttributes.Controls.Clear();
            Rock.Attribute.Helper.AddEditControls( MockBenevolenceResult, phResultAttributes, true, valResultsSummary.ValidationGroup, 2 );

            mdAddResult.SaveButtonText = "Add";
            mdAddResult.Show();
        }

        /// <summary>
        /// Handles the RowSelected event of the gResults control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        protected void gResults_RowSelected( object sender, RowEventArgs e )
        {
            Guid? infoGuid = e.RowKeyValue as Guid?;
            List<BenevolenceResultInfo> resultList = BenevolenceResultsState;
            var resultInfo = resultList.FirstOrDefault( r => r.TempGuid == infoGuid );
            if ( resultInfo != null )
            {
                dvpResultType.Items.Clear();
                dvpResultType.AutoPostBack = false;
                dvpResultType.Required = true;
                dvpResultType.DefinedTypeId = DefinedTypeCache.Get( new Guid( Rock.SystemGuid.DefinedType.BENEVOLENCE_RESULT_TYPE ) ).Id;
                dvpResultType.SetValue( resultInfo.ResultTypeValueId );
                dtbResultSummary.Text = resultInfo.ResultSummary;
                dtbAmount.Value = resultInfo.Amount;
                hfInfoGuid.Value = e.RowKeyValue.ToString();

                phResultAttributes.Controls.Clear();
                Rock.Attribute.Helper.AddEditControls( resultInfo, phResultAttributes, true, valResultsSummary.ValidationGroup, 2 );

                mdAddResult.SaveButtonText = "Update";
                mdAddResult.Show();
            }
        }

        /// <summary>
        /// Handles the DeleteClick event of the gResult control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        protected void gResults_Delete( object sender, RowEventArgs e )
        {
            Guid? infoGuid = e.RowKeyValue as Guid?;
            List<BenevolenceResultInfo> resultList = BenevolenceResultsState;
            var resultInfo = resultList.FirstOrDefault( r => r.TempGuid == infoGuid );
            if ( resultInfo != null )
            {
                resultList.Remove( resultInfo );
            }

            BenevolenceResultsState = resultList;
            BindGridFromViewState();
        }

        /// <summary>
        /// Handles the AddClick event of the mdAddResult control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        protected void btnAddResults_Click( object sender, EventArgs e )
        {
            int? resultType = dvpResultType.SelectedItem.Value.AsIntegerOrNull();
            List<BenevolenceResultInfo> benevolenceResultInfoViewStateList = BenevolenceResultsState;
            Guid? infoGuid = hfInfoGuid.Value.AsGuidOrNull();

            if ( infoGuid == null )
            {
                infoGuid = Guid.NewGuid();
            }
            var resultInfo = benevolenceResultInfoViewStateList.FirstOrDefault( r => r.TempGuid == infoGuid );
            if ( resultInfo != null )
            {
                resultInfo.Amount = dtbAmount.Value;
                resultInfo.ResultSummary = dtbResultSummary.Text;
                if ( resultType != null )
                {
                    resultInfo.ResultTypeValueId = resultType.Value;
                }

                resultInfo.ResultTypeName = dvpResultType.SelectedItem.Text;
                Rock.Attribute.Helper.GetEditValues( phResultAttributes, resultInfo );
            }
            else
            {
                BenevolenceResultInfo benevolenceResultInfo = new BenevolenceResultInfo();

                //We need the attributes and values so that we can populate them later

                benevolenceResultInfo.Attributes = MockBenevolenceResult.Attributes;
                benevolenceResultInfo.AttributeValues = MockBenevolenceResult.AttributeValues;

                benevolenceResultInfo.Amount = dtbAmount.Value;

                benevolenceResultInfo.ResultSummary = dtbResultSummary.Text;
                if ( resultType != null )
                {
                    benevolenceResultInfo.ResultTypeValueId = resultType.Value;
                }

                benevolenceResultInfo.ResultTypeName = dvpResultType.SelectedItem.Text;
                benevolenceResultInfo.TempGuid = Guid.NewGuid();
                Rock.Attribute.Helper.GetEditValues( phResultAttributes, benevolenceResultInfo );
                benevolenceResultInfoViewStateList.Add( benevolenceResultInfo );
            }


            BenevolenceResultsState = benevolenceResultInfoViewStateList;

            mdAddResult.Hide();
            pnlView.Visible = true;
            hfInfoGuid.Value = null;
            BindGridFromViewState();
        }

        /// <summary>
        /// Handles the Click event of the lbSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbSave_Click( object sender, EventArgs e )
        {
            if ( Page.IsValid )
            {
                RockContext rockContext = new RockContext();
                BenevolenceRequestService benevolenceRequestService = new BenevolenceRequestService( rockContext );
                BenevolenceResultService benevolenceResultService = new BenevolenceResultService( rockContext );

                BenevolenceRequest benevolenceRequest = null;
                int benevolenceRequestId = PageParameter( PageParameterKey.BenevolenceRequestId ).AsInteger();

                if ( !benevolenceRequestId.Equals( 0 ) )
                {
                    benevolenceRequest = benevolenceRequestService.Get( benevolenceRequestId );
                }

                if ( benevolenceRequest == null )
                {
                    benevolenceRequest = new BenevolenceRequest { Id = 0 };
                }

                benevolenceRequest.FirstName = dtbFirstName.Text;
                benevolenceRequest.LastName = dtbLastName.Text;
                benevolenceRequest.Email = ebEmail.Text;
                benevolenceRequest.RequestText = dtbRequestText.Text;
                benevolenceRequest.ResultSummary = dtbSummary.Text;
                benevolenceRequest.CampusId = cpCampus.SelectedCampusId;
                benevolenceRequest.ProvidedNextSteps = dtbProvidedNextSteps.Text;
                benevolenceRequest.GovernmentId = dtbGovernmentId.Text;

                if ( lapAddress.Location != null )
                {
                    benevolenceRequest.LocationId = lapAddress.Location.Id;
                }

                benevolenceRequest.RequestedByPersonAliasId = ppPerson.PersonAliasId;

                if ( _caseWorkerGroupGuid.HasValue )
                {
                    benevolenceRequest.CaseWorkerPersonAliasId = ddlCaseWorker.SelectedValue.AsIntegerOrNull();
                }
                else
                {
                    benevolenceRequest.CaseWorkerPersonAliasId = ppCaseWorker.PersonAliasId;
                }

                benevolenceRequest.RequestStatusValueId = dvpRequestStatus.SelectedValue.AsIntegerOrNull();
                benevolenceRequest.ConnectionStatusValueId = dvpConnectionStatus.SelectedValue.AsIntegerOrNull();

                if ( dpRequestDate.SelectedDate.HasValue )
                {
                    benevolenceRequest.RequestDateTime = dpRequestDate.SelectedDate.Value;
                }

                benevolenceRequest.HomePhoneNumber = pnbHomePhone.Number;
                benevolenceRequest.CellPhoneNumber = pnbCellPhone.Number;
                benevolenceRequest.WorkPhoneNumber = pnbWorkPhone.Number;

                List<BenevolenceResultInfo> resultListUI = BenevolenceResultsState;
                var resultListDB = benevolenceRequest.BenevolenceResults.ToList();

                // remove any Benevolence Results that were removed in the UI
                foreach ( BenevolenceResult resultDB in resultListDB )
                {
                    if ( !resultListUI.Any( r => r.ResultId == resultDB.Id ) )
                    {
                        benevolenceRequest.BenevolenceResults.Remove( resultDB );
                        benevolenceResultService.Delete( resultDB );
                    }
                }

                // add any Benevolence Results that were added in the UI
                foreach ( BenevolenceResultInfo resultUI in resultListUI )
                {
                    var resultDB = resultListDB.FirstOrDefault( r => r.Guid == resultUI.TempGuid );
                    if ( resultDB == null )
                    {
                        resultDB = new BenevolenceResult();
                        resultDB.BenevolenceRequestId = benevolenceRequest.Id;
                        resultDB.Guid = resultUI.TempGuid;
                        benevolenceRequest.BenevolenceResults.Add( resultDB );
                    }

                    resultDB.Amount = resultUI.Amount;
                    resultDB.ResultSummary = resultUI.ResultSummary;
                    resultDB.ResultTypeValueId = resultUI.ResultTypeValueId;

                    resultDB.Attributes = resultUI.Attributes;
                    resultDB.AttributeValues = resultUI.AttributeValues;
                }

                if ( benevolenceRequest.IsValid )
                {
                    if ( benevolenceRequest.Id.Equals( 0 ) )
                    {
                        benevolenceRequestService.Add( benevolenceRequest );
                    }

                    // get attributes
                    benevolenceRequest.LoadAttributes();
                    Rock.Attribute.Helper.GetEditValues( phAttributes, benevolenceRequest );

                    rockContext.WrapTransaction( () =>
                    {
                        rockContext.SaveChanges();
                        benevolenceRequest.SaveAttributeValues( rockContext );
                        benevolenceRequest.BenevolenceResults.ToList().ForEach( r => r.SaveAttributeValues( rockContext ) );
                    } );

                    // update related documents
                    var documentsService = new BenevolenceRequestDocumentService( rockContext );
                    var binaryFileService = new BinaryFileService( rockContext );

                    // delete any images that were removed
                    var orphanedBinaryFileIds = new List<int>();
                    var documentsInDb = documentsService.Queryable().Where( b => b.BenevolenceRequestId == benevolenceRequest.Id ).ToList();

                    foreach ( var document in documentsInDb.Where( i => !DocumentsState.Contains( i.BinaryFileId ) ) )
                    {
                        orphanedBinaryFileIds.Add( document.BinaryFileId );
                        documentsService.Delete( document );
                    }

                    // save documents
                    int documentOrder = 0;
                    foreach ( var binaryFileId in DocumentsState )
                    {
                        // Add or Update the activity type
                        var document = documentsInDb.FirstOrDefault( i => i.BinaryFileId == binaryFileId );
                        if ( document == null )
                        {
                            document = new BenevolenceRequestDocument();
                            document.BenevolenceRequestId = benevolenceRequest.Id;
                            benevolenceRequest.Documents.Add( document );
                        }
                        document.BinaryFileId = binaryFileId;
                        document.Order = documentOrder;
                        documentOrder++;
                    }

                    // Make sure updated binary files are not temporarytemporary
                    foreach ( var binaryFile in binaryFileService.Queryable().Where( f => DocumentsState.Contains( f.Id ) ) )
                    {
                        binaryFile.IsTemporary = false;
                    }

                    // Delete any orphaned images
                    foreach ( var binaryFile in binaryFileService.Queryable().Where( f => orphanedBinaryFileIds.Contains( f.Id ) ) )
                    {
                        binaryFile.IsTemporary = true;
                    }

                    rockContext.SaveChanges();

                    // redirect back to parent
                    var personId = this.PageParameter( "PersonId" ).AsIntegerOrNull();
                    var qryParams = new Dictionary<string, string>();
                    if ( personId.HasValue )
                    {
                        qryParams.Add( "PersonId", personId.ToString() );
                    }

                    NavigateToParentPage( qryParams );
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the lbCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbCancel_Click( object sender, EventArgs e )
        {
            var personId = this.PageParameter( "PersonId" ).AsIntegerOrNull();
            var qryParams = new Dictionary<string, string>();
            if ( personId.HasValue )
            {
                qryParams.Add( "PersonId", personId.ToString() );
            }

            NavigateToParentPage( qryParams );
        }

        /// <summary>
        /// Handles the Click event of the lbPrint control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbPrint_Click( object sender, EventArgs e )
        {
            var benevolenceRequestId = this.PageParameter( PageParameterKey.BenevolenceRequestId ).AsIntegerOrNull();
            if ( benevolenceRequestId.HasValue && !benevolenceRequestId.Equals( 0 ) && !string.IsNullOrEmpty( GetAttributeValue( "BenevolenceRequestStatementPage" ) ) )
            {
                NavigateToLinkedPage( "BenevolenceRequestStatementPage", new Dictionary<string, string> { { PageParameterKey.BenevolenceRequestId, benevolenceRequestId.ToString() } } );
            }
        }

        /// <summary>
        /// Handles the SelectPerson event of the ppPerson control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ppPerson_SelectPerson( object sender, EventArgs e )
        {
            if ( ppPerson.PersonId != null )
            {
                Person person = new PersonService( new RockContext() ).Get( ppPerson.PersonId.Value );
                if ( person != null )
                {
                    // Make sure that the FirstName box gets either FirstName or NickName of person. 
                    if ( !string.IsNullOrWhiteSpace( person.FirstName ) )
                    {
                        dtbFirstName.Text = person.FirstName;
                    }
                    else if ( !string.IsNullOrWhiteSpace( person.NickName ) )
                    {
                        dtbFirstName.Text = person.NickName;
                    }

                    //If both FirstName and NickName are blank, let them edit it manually
                    dtbFirstName.Enabled = string.IsNullOrWhiteSpace( dtbFirstName.Text );

                    dtbLastName.Text = person.LastName;
                    //If both LastName is blank, let them edit it manually
                    dtbLastName.Enabled = string.IsNullOrWhiteSpace( dtbLastName.Text );
                    ;

                    dvpConnectionStatus.SetValue( person.ConnectionStatusValueId );
                    dvpConnectionStatus.Enabled = false;

                    var homePhoneType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid() );
                    if ( homePhoneType != null )
                    {
                        var homePhone = person.PhoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == homePhoneType.Id );
                        if ( homePhone != null )
                        {
                            pnbHomePhone.Text = homePhone.NumberFormatted;
                            pnbHomePhone.Enabled = false;
                        }
                    }

                    var mobilePhoneType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid() );
                    if ( mobilePhoneType != null )
                    {
                        var mobileNumber = person.PhoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == mobilePhoneType.Id );
                        if ( mobileNumber != null )
                        {
                            pnbCellPhone.Text = mobileNumber.NumberFormatted;
                            pnbCellPhone.Enabled = false;
                        }
                    }

                    var workPhoneType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK.AsGuid() );
                    if ( workPhoneType != null )
                    {
                        var workPhone = person.PhoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == workPhoneType.Id );
                        if ( workPhone != null )
                        {
                            pnbWorkPhone.Text = workPhone.NumberFormatted;
                            pnbWorkPhone.Enabled = false;
                        }
                    }

                    ebEmail.Text = person.Email;
                    ebEmail.Enabled = false;

                    lapAddress.SetValue( person.GetHomeLocation() );
                    lapAddress.Enabled = false;

                    // set the campus but not on page load (e will be null) unless from the person profile page (in which case BenevolenceRequestId in the query string will be 0)
                    int? requestId = PageParameter( PageParameterKey.BenevolenceRequestId ).AsIntegerOrNull();

                    if ( !cpCampus.SelectedCampusId.HasValue && ( e != null || ( requestId.HasValue && requestId == 0 ) ) )
                    {
                        var personCampus = person.GetCampus();
                        cpCampus.SelectedCampusId = personCampus != null ? personCampus.Id : ( int? ) null;
                    }
                }
            }
            else
            {
                dtbFirstName.Enabled = true;
                dtbLastName.Enabled = true;
                dvpConnectionStatus.Enabled = true;
                pnbHomePhone.Enabled = true;
                pnbCellPhone.Enabled = true;
                pnbWorkPhone.Enabled = true;
                ebEmail.Enabled = true;
                lapAddress.Enabled = true;
            }
        }

        protected void fileUpDoc_FileUploaded( object sender, EventArgs e )
        {
            var fileUpDoc = ( Rock.Web.UI.Controls.FileUploader ) sender;

            if ( fileUpDoc.BinaryFileId.HasValue )
            {
                DocumentsState.Add( fileUpDoc.BinaryFileId.Value );
                BindDocuments( true );
            }
        }

        /// <summary>
        /// Handles the FileRemoved event of the fileUpDoc control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="FileUploaderEventArgs"/> instance containing the event data.</param>
        protected void fileUpDoc_FileRemoved( object sender, FileUploaderEventArgs e )
        {
            var fileUpDoc = ( Rock.Web.UI.Controls.FileUploader ) sender;
            if ( e.BinaryFileId.HasValue )
            {
                DocumentsState.Remove( e.BinaryFileId.Value );
                BindDocuments( true );
            }
        }

        /// <summary>
        /// Handles the ItemDataBound event of the DlDocuments control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataListItemEventArgs"/> instance containing the event data.</param>
        private void DlDocuments_ItemDataBound( object sender, DataListItemEventArgs e )
        {
            Guid binaryFileTypeGuid = Rock.SystemGuid.BinaryFiletype.BENEVOLENCE_REQUEST_DOCUMENTS.AsGuid();
            var fileupDoc = e.Item.FindControl( "fileupDoc" ) as Rock.Web.UI.Controls.FileUploader;
            if ( fileupDoc != null )
            {
                fileupDoc.BinaryFileTypeGuid = binaryFileTypeGuid;
            }
        }


        #endregion

        #region Methods

        /// <summary>
        /// Binds the documents.
        /// </summary>
        /// <param name="canEdit">if set to <c>true</c> [can edit].</param>
        private void BindDocuments( bool canEdit )
        {
            var ds = DocumentsState.ToList();

            if ( ds.Count() < 6 )
            {
                ds.Add( 0 );
            }

            dlDocuments.DataSource = ds;
            dlDocuments.DataBind();
        }

        /// <summary>
        /// Shows the detail.
        /// </summary>
        /// <param name="benevolenceRequestId">The benevolence request identifier</param>
        public void ShowDetail( int benevolenceRequestId )
        {
            BenevolenceRequest benevolenceRequest = null;
            var rockContext = new RockContext();
            BenevolenceRequestService benevolenceRequestService = new BenevolenceRequestService( rockContext );
            if ( !benevolenceRequestId.Equals( 0 ) )
            {
                benevolenceRequest = benevolenceRequestService.Get( benevolenceRequestId );
                pdAuditDetails.SetEntity( benevolenceRequest, ResolveRockUrl( "~" ) );
            }

            if ( benevolenceRequest == null )
            {
                benevolenceRequest = new BenevolenceRequest { Id = 0 };
                benevolenceRequest.RequestDateTime = RockDateTime.Now;
                var personId = this.PageParameter( "PersonId" ).AsIntegerOrNull();
                if ( personId.HasValue )
                {
                    var person = new PersonService( rockContext ).Get( personId.Value );
                    if ( person != null )
                    {
                        benevolenceRequest.RequestedByPersonAliasId = person.PrimaryAliasId;
                        benevolenceRequest.RequestedByPersonAlias = person.PrimaryAlias;
                    }
                }
                // hide the panel drawer that show created and last modified dates
                pdAuditDetails.Visible = false;
            }

            dtbFirstName.Text = benevolenceRequest.FirstName;
            dtbLastName.Text = benevolenceRequest.LastName;
            dtbGovernmentId.Text = benevolenceRequest.GovernmentId;
            ebEmail.Text = benevolenceRequest.Email;
            dtbRequestText.Text = benevolenceRequest.RequestText;
            dtbSummary.Text = benevolenceRequest.ResultSummary;
            dtbProvidedNextSteps.Text = benevolenceRequest.ProvidedNextSteps;
            dpRequestDate.SelectedDate = benevolenceRequest.RequestDateTime;

            if ( benevolenceRequest.Campus != null )
            {
                cpCampus.SelectedCampusId = benevolenceRequest.CampusId;
            }
            else
            {
                cpCampus.SelectedIndex = 0;
            }

            if ( benevolenceRequest.RequestedByPersonAlias != null )
            {
                ppPerson.SetValue( benevolenceRequest.RequestedByPersonAlias.Person );
            }
            else
            {
                ppPerson.SetValue( null );
            }

            if ( benevolenceRequest.HomePhoneNumber != null )
            {
                pnbHomePhone.Text = benevolenceRequest.HomePhoneNumber;
            }

            if ( benevolenceRequest.CellPhoneNumber != null )
            {
                pnbCellPhone.Text = benevolenceRequest.CellPhoneNumber;
            }

            if ( benevolenceRequest.WorkPhoneNumber != null )
            {
                pnbWorkPhone.Text = benevolenceRequest.WorkPhoneNumber;
            }

            lapAddress.SetValue( benevolenceRequest.Location );

            LoadDropDowns( benevolenceRequest );

            if ( benevolenceRequest.RequestStatusValueId != null )
            {
                dvpRequestStatus.SetValue( benevolenceRequest.RequestStatusValueId );

                if ( benevolenceRequest.RequestStatusValue.Value == "Approved" )
                {
                    hlStatus.Text = "Approved";
                    hlStatus.LabelType = LabelType.Success;
                }

                if ( benevolenceRequest.RequestStatusValue.Value == "Denied" )
                {
                    hlStatus.Text = "Denied";
                    hlStatus.LabelType = LabelType.Danger;
                }
            }

            if ( benevolenceRequest.ConnectionStatusValueId != null )
            {
                dvpConnectionStatus.SetValue( benevolenceRequest.ConnectionStatusValueId );
            }

            if ( _caseWorkerGroupGuid.HasValue )
            {
                ddlCaseWorker.SetValue( benevolenceRequest.CaseWorkerPersonAliasId );
            }
            else
            {
                if ( benevolenceRequest.CaseWorkerPersonAlias != null )
                {
                    ppCaseWorker.SetValue( benevolenceRequest.CaseWorkerPersonAlias.Person );
                }
                else
                {
                    ppCaseWorker.SetValue( null );
                }
            }

            BindGridFromViewState();

            DocumentsState = benevolenceRequest.Documents.OrderBy( s => s.Order ).Select( s => s.BinaryFileId ).ToList();
            BindDocuments( true );

            benevolenceRequest.LoadAttributes();
            Rock.Attribute.Helper.AddEditControls( benevolenceRequest, phAttributes, true, BlockValidationGroup, 2 );

            // call the OnSelectPerson of the person picker which will update the UI based on the selected person
            ppPerson_SelectPerson( null, null );

            hfBenevolenceRequestId.Value = benevolenceRequest.Id.ToString();
        }

        /// <summary>
        /// Binds the grid.
        /// </summary>
        private void BindGridFromViewState()
        {
            List<BenevolenceResultInfo> benevolenceResultInfoViewStateList = BenevolenceResultsState;
            gResults.DataSource = benevolenceResultInfoViewStateList;
            gResults.DataBind();
        }

        /// <summary>
        /// Loads the drop downs.
        /// </summary>
        private void LoadDropDowns( BenevolenceRequest benevolenceRequest )
        {
            dvpRequestStatus.DefinedTypeId = DefinedTypeCache.Get( new Guid( Rock.SystemGuid.DefinedType.BENEVOLENCE_REQUEST_STATUS ) ).Id;
            dvpConnectionStatus.DefinedTypeId = DefinedTypeCache.Get( new Guid( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS ) ).Id;

            if ( _caseWorkerGroupGuid.HasValue )
            {
                var personList = new GroupMemberService( new RockContext() )
                    .Queryable( "Person, Group" )
                    .Where( gm => gm.Group.Guid == _caseWorkerGroupGuid.Value )
                    .Select( gm => gm.Person )
                    .ToList();

                string caseWorkerPersonAliasValue = benevolenceRequest.CaseWorkerPersonAliasId.ToString();
                if ( benevolenceRequest.CaseWorkerPersonAlias != null &&
                    benevolenceRequest.CaseWorkerPersonAlias.Person != null &&
                    !personList.Select( p => p.Id ).ToList().Contains( benevolenceRequest.CaseWorkerPersonAlias.Person.Id ) )
                {
                    personList.Add( benevolenceRequest.CaseWorkerPersonAlias.Person );
                }

                ddlCaseWorker.DataSource = personList.OrderBy( p => p.NickName ).ThenBy( p => p.LastName ).ToList();
                ddlCaseWorker.DataTextField = "FullName";
                ddlCaseWorker.DataValueField = "PrimaryAliasId";
                ddlCaseWorker.DataBind();
                ddlCaseWorker.Items.Insert( 0, new ListItem() );

                ppCaseWorker.Visible = false;
                ddlCaseWorker.Visible = true;
            }
            else
            {
                ppCaseWorker.Visible = true;
                ddlCaseWorker.Visible = false;
            }
        }

        #endregion

        #region BenevolenceResultInfo

        /// <summary>
        /// The class used to store BenevolenceResult info.
        /// </summary>
        [Serializable]
        public class BenevolenceResultInfo : IHasAttributes
        {
            [DataMember]
            public int? ResultId { get; set; }

            [DataMember]
            public int ResultTypeValueId { get; set; }

            [DataMember]
            public string ResultTypeName { get; set; }

            [DataMember]
            public decimal? Amount { get; set; }

            [DataMember]
            public Guid TempGuid { get; set; }

            [DataMember]
            public string ResultSummary { get; set; }

            public int Id
            {
                get
                {
                    return ResultId ?? 0;
                }
            }

            [DataMember]
            public Dictionary<string, AttributeCache> Attributes { get; set; }

            [DataMember]
            public Dictionary<string, AttributeValueCache> AttributeValues { get; set; }

            public Dictionary<string, string> AttributeValueDefaults { get { return null; } }

            public string GetAttributeValue( string key )
            {
                if ( this.AttributeValues != null &&
                    this.AttributeValues.ContainsKey( key ) )
                {
                    return this.AttributeValues[key].Value;
                }

                if ( this.Attributes != null &&
                    this.Attributes.ContainsKey( key ) )
                {
                    return this.Attributes[key].DefaultValue;
                }

                return null;
            }

            public List<string> GetAttributeValues( string key )
            {
                string value = GetAttributeValue( key );
                if ( !string.IsNullOrWhiteSpace( value ) )
                {
                    return value.SplitDelimitedValues().ToList();
                }

                return new List<string>();
            }

            public void SetAttributeValue( string key, string value )
            {
                if ( this.AttributeValues != null &&
                this.AttributeValues.ContainsKey( key ) )
                {
                    this.AttributeValues[key].Value = value;
                }
            }
        }

        #endregion


    }
}
