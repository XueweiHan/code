using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X
{
    internal static class Test
    {
        public static void Test1()
        {
            C c = new C()
            {
                d0 = new D()
                {
                    e1 = new F {x = 1}
                },
                d1 = new D()
                {
                    e1 = new F {x = 2}
                },
                d2 = new D()
                {
                    e1 = new F {x = 3}
                },
            };
        }
    }

}


namespace X
{

    interface IA
    {
        ID d0 { get; set; }
    };

    interface IB : IA
    {
        ID d1 { get; set; }
    };

    interface IC : IB
    {
        ID d2 { get; set; }
    };


    interface ID
    {
        IE e1 { get; set; }
    };

    interface IE
    {
        int x { get; set; }
    };

    interface IF: IE
    { }

    //class A : IA
    //{
    //    public ID d0 { get; set; }
    //};

    //class B : IB
    //{
    //    public ID d0 { get; set; }
    //    public ID d1 { get; set; }
    //}

    class C : IC
    {
        public ID d0 { get; set; }
        public ID d1 { get; set; }
        public ID d2 { get; set; }
    }

    class D : ID
    {
        public IE e1 { get; set; }

    }

    class E : IE
    {
        public int x { get; set; }
    }

    class F : IF
    {
        public int x { get; set; }
    }

}
