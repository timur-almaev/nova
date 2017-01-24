using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ssi.Types
{
    public class Point : IObservableListItem
    {
        private double x;
        private double y;
        private string label;

        private double X
        {
            get { return x; }
            set
            {
                x = value;
                OnPropertyChanged("X");
            }
        }

        private double Y
        {
            get { return y; }
            set
            {
                y = value;
                OnPropertyChanged("Y");
            }
        }

        private string Label
        {
            get { return label; }
            set
            {
                label = value;
                OnPropertyChanged("Label");
            }
        }

        public Point(double x, double y, string label)
        {
            this.x = x;
            this.y = y;
            this.label = label;
        }

        public class PointComparer : IComparer<Point>
        {
            int IComparer<Point>.Compare(Point a, Point b)
            {


                if (a.x + a.y < b.x + b.y)
                {
                    return -1;
                }
                else if (a.x + a.y > b.x + b.y)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}