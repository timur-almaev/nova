﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections;
using System.Collections.ObjectModel;

namespace ssi
{
    /// <summary>
    /// Interaction logic for GeometricListControl.xaml
    /// </summary>
    public partial class GeometricListControl : UserControl
    {
        private GridViewColumnHeader listViewSortCol = null;
        private ListViewSortAdorner listViewSortAdorner = null;

        public GeometricListControl()
        {
            InitializeComponent();
        }

        private void MenuItemDeleteClick(object sender, RoutedEventArgs e)
        {
            ///impliment deltetion of points, ...
            /*if (AnnoTierStatic.Selected.currentAnnoType == AnnoScheme.TYPE.FREE || AnnoTierStatic.Selected.currentAnnoType == AnnoScheme.TYPE.DISCRETE)
            {
                if (geometricDataGrid.SelectedItems.Count > 0)
                {
                    AnnoListItem[] selected = new AnnoListItem[geometricDataGrid.SelectedItems.Count];
                    geometricDataGrid.SelectedItems.CopyTo(selected, 0);
                    geometricDataGrid.SelectedIndex = -1;

                    AnnoTier track = AnnoTierStatic.Selected;
                    foreach (AnnoListItem s in selected)
                    {
                        AnnoTierLabel segment = track.getSegment(s);
                        if (segment != null)
                        {
                            track.remSegment(segment);
                        }
                    }
                }
            }*/
        }

        private void editTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
        }

        private void MenuItemSetConfidenceZeroClick(object sender, RoutedEventArgs e)
        {
            if (geometricDataGrid.SelectedItems.Count != 0)
            {
                //if (geometricDataGrid.SelectedItem.GetType == Point)
                {

                }
                foreach (AnnoListItem s in geometricDataGrid.SelectedItems)
                {
                    s.Confidence = 0.0;
                }
              
            }
        }

        private void MenuItemSetConfidenceOneClick(object sender, RoutedEventArgs e)
        {
            if (geometricDataGrid.SelectedItems.Count != 0)
            {
                foreach (AnnoListItem s in geometricDataGrid.SelectedItems)
                {
                    s.Confidence = 1.0;
                }

            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (sender as GridViewColumnHeader);
            if (column.Tag != null)
            {                
                string sortBy = column.Tag.ToString();
                if (sortBy == "Label")
                {

                    if (listViewSortCol != null)
                    {
                        AdornerLayer.GetAdornerLayer(listViewSortCol).Remove(listViewSortAdorner);
                        geometricDataGrid.Items.SortDescriptions.Clear();
                    }

                    if (listViewSortCol == null)
                    {
                        listViewSortCol = column;
                        listViewSortAdorner = new ListViewSortAdorner(listViewSortCol, ListSortDirection.Ascending);
                        AdornerLayer.GetAdornerLayer(listViewSortCol).Add(listViewSortAdorner);
                        geometricDataGrid.Items.SortDescriptions.Add(new SortDescription(sortBy, ListSortDirection.Ascending));
                    }
                    else if (listViewSortAdorner.Direction == ListSortDirection.Ascending)
                    {
                        listViewSortAdorner = new ListViewSortAdorner(listViewSortCol, ListSortDirection.Descending);
                        AdornerLayer.GetAdornerLayer(listViewSortCol).Add(listViewSortAdorner);
                        geometricDataGrid.Items.SortDescriptions.Add(new SortDescription(sortBy, ListSortDirection.Descending));
                    }
                    else
                    {
                        listViewSortCol = null;
                    }
                }               
            }     
        }

    }
}