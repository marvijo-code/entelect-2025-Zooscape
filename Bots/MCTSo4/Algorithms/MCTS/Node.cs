using System;
using System.Collections.Generic;
using System.Linq;
using MCTSo4.Enums;
using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS
{
    public class MctsNode
    {
        public MCTSGameState State { get; }
        public MctsNode? Parent { get; }
        public Move? Move { get; }
        public List<MctsNode> Children { get; }
        public List<Move> UntriedActions { get; }
        public double Wins { get; set; }
        public int Visits { get; set; }
        public double UctConstant { get; }

        public MctsNode(MCTSGameState state, MctsNode? parent, Move? move, double uctConstant)
        {
            State = state;
            Parent = parent;
            Move = move;
            UctConstant = uctConstant;
            Children = new List<MctsNode>();
            UntriedActions = state.GetLegalMoves();
            Wins = 0;
            Visits = 0;
        }

        public bool IsFullyExpanded => UntriedActions.Count == 0;
        public bool IsTerminal => State.IsTerminal();

        public double UctValue()
        {
            if (Visits == 0)
                return double.MaxValue;
            if (Parent == null || Parent.Visits == 0)
                return Wins / Visits;
            return (Wins / Visits) + UctConstant * Math.Sqrt(2 * Math.Log(Parent.Visits) / Visits);
        }

        public MctsNode BestChild()
        {
            return Children.OrderByDescending(c => c.UctValue()).First();
        }
    }
}
