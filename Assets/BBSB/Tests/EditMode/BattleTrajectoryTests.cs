using System;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BattleTrajectoryTests
    {
        [Test]
        public void MissLandingStartsAtThePunchAndPlacesTheDollsFeetOnTheGround()
        {
            foreach (double playerHeight in new[] { 120.0, 180.0, 240.0 })
            foreach (double scale in new[] { .75, 1.0, 1.5 })
            foreach (double pivot in new[] { 0.0, .5, 1.0 })
            {
                double ground = 100, bodyHeight = .52 * playerHeight * scale;
                var contact = new BattlePathPoint(200, ground + .46 * playerHeight);
                var start = BattleTrajectory.MissLanding(contact, ground, bodyHeight, pivot, playerHeight, 0);
                Check.Equal(contact.X, start.X); Check.Equal(contact.Y, start.Y);
                var end = BattleTrajectory.MissLanding(contact, ground, bodyHeight, pivot, playerHeight, 1);
                Check.True(Math.Abs(end.Y - bodyHeight * pivot - ground) < 1e-8);
                Check.True(end.X < contact.X);
                var beyond = BattleTrajectory.MissLanding(contact, ground, bodyHeight, pivot, playerHeight, 2);
                Check.Equal(end.Y, beyond.Y); Check.Equal(end.X, beyond.X);
            }
        }

        [Test]
        public void IncomingAndCounterPathsMeetActorsAndStayVisuallySeparated()
        {
            foreach (int count in new[] { 1, 2, 3 })
            for (int i = 0; i < count; i++)
            foreach (double advance in new[] { 0.0, 1.0 })
            {
                var monster = BattleStageLayout.Monster(i, count, .14, .15, advance);
                double targetY = monster.Y + .10;
                var start = BattleTrajectory.Incoming(monster.X, targetY, .14, .31, 0);
                var arrived = BattleTrajectory.Incoming(monster.X, targetY, .14, .31, 1);
                Check.Equal(monster.X, start.X); Check.Equal(targetY, start.Y);
                Check.True(Math.Abs(arrived.X - .14) < 1e-9); Check.Equal(.31, arrived.Y);
                var returnStart = BattleTrajectory.Counter(.14, .31, monster.X, targetY, 0);
                var returnEnd = BattleTrajectory.Counter(.14, .31, monster.X, targetY, 1);
                Check.Equal(.14, returnStart.X); Check.Equal(.31, returnStart.Y);
                Check.True(Math.Abs(returnEnd.X - monster.X) < 1e-9); Check.Equal(targetY, returnEnd.Y);
                for (int step = 1; step < 10; step++)
                {
                    double t = step / 10.0;
                    var incoming = BattleTrajectory.Incoming(monster.X, targetY, .14, .31, t);
                    var outgoing = BattleTrajectory.Counter(.14, .31, monster.X, targetY, 1 - t);
                    Check.True(Math.Abs(incoming.X - outgoing.X) < 1e-9);
                    Check.True(outgoing.Y > incoming.Y && outgoing.Y < .8);
                    Check.True(incoming.Y > 0 && incoming.Y < .5);
                }
            }
        }

        [Test]
        public void SmallerEnemyFormationsLeaveTheCentralTravelSpaceOpen()
        {
            foreach (int count in new[] { 1, 2, 3 })
            foreach (double height in new[] { 720.0, 800.0 })
            for (int i = 0; i < count; i++)
            foreach (double advance in new[] { 0.0, 1.0 })
            {
                var monster = BattleStageLayout.Monster(i, count, .14, .15, advance);
                double side = BattleStageLayout.MonsterSize(count, 1280, height, monster.Scale);
                Check.True(monster.X - side / 2560 > .64, "The left and central area must stay available for incoming attacks.");
                Check.True(monster.X + side / 2560 < 1);
                Check.True(side / height < .25);
            }
        }
    }
}
