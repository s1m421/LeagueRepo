﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace OneKeyToWin_AIO_Sebby
{
    class Ashe
    {
        private Menu Config = Program.Config;
        public static Orbwalking.Orbwalker Orbwalker = Program.Orbwalker;
        public Spell Q, W, E, R;
        public float QMANA, WMANA, EMANA, RMANA;

        public Obj_AI_Hero Player
        {
            get { return ObjectManager.Player; }
        }

        public void LoadOKTW()
        {
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W, 1200);
            E = new Spell(SpellSlot.E, 2500);
            R = new Spell(SpellSlot.R, 3000f);

            W.SetSkillshot(0.5f, 50f , 1000f, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.25f, 299f, 1400f, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.25f, 130f, 1600f, false, SkillshotType.SkillshotLine);
            LoadMenuOKTW();

            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Orbwalking.BeforeAttack += BeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += Interrupter_OnPossibleToInterrupt;
        }

        private void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (R.IsReady() )
            {
                var Target = (Obj_AI_Hero)gapcloser.Sender;
                if (Config.Item("GapCloser" + Target.BaseSkinName).GetValue<bool>() && Target.IsValidTarget(800))
                {
                    R.Cast(Target, true);
                    Program.debug("AGC");
                }
            }
        }

        private void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            LogicQ();
        }

        private void Interrupter_OnPossibleToInterrupt(Obj_AI_Hero unit, InterruptableSpell spell)
        {
            if (Config.Item("autoRinter").GetValue<bool>() && R.IsReady() && unit.IsValidTarget(R.Range))
                R.Cast(unit);
        }


        private void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("wRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (W.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
            }
        }

        private void Game_OnUpdate(EventArgs args)
        {

            if (R.IsReady())
            {
                if (Config.Item("useR").GetValue<KeyBind>().Active)
                {
                    var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
                    if (t.IsValidTarget())
                        R.Cast(t, true, true);
                }
            }
            GetQStacks();
            if (Program.LagFree(1))
            {
                SetMana();
            }

            if (Program.LagFree(3) && W.IsReady() && !Player.IsWindingUp)
                LogicW();

            if (Program.LagFree(4) && R.IsReady())
                LogicR();
        }

        private void LogicR()
        {
            if (Config.Item("autoR").GetValue<bool>())
            {
                bool cast = false;
                foreach (var target in ObjectManager.Get<Obj_AI_Hero>().Where(target => target.IsValidTarget(R.Range) && target.IsEnemy && Program.ValidUlt(target)))
                {
                    if (Config.Item("autoRinter").GetValue<bool>() && target.IsChannelingImportantSpell())
                        R.Cast(target);

                    float predictedHealth = target.Health + target.HPRegenRate * 2;
                    var Rdmg = R.GetDamage(target);
                    if (target.CountEnemiesInRange(250) > 2 && Config.Item("autoRaoe").GetValue<bool>() && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                        Program.CastSpell(R, target);
                    if (Rdmg > predictedHealth && target.CountAlliesInRange(600) == 0 && target.Distance(Player.Position) > 1000)
                    {
                        cast = true;
                        PredictionOutput output = R.GetPrediction(target);
                        Vector2 direction = output.CastPosition.To2D() - Player.Position.To2D();
                        direction.Normalize();
                        List<Obj_AI_Hero> enemies = ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget()).ToList();
                        foreach (var enemy in enemies)
                        {
                            if (enemy.SkinName == target.SkinName || !cast)
                                continue;
                            PredictionOutput prediction = R.GetPrediction(enemy);
                            Vector3 predictedPosition = prediction.CastPosition;
                            Vector3 v = output.CastPosition - Player.ServerPosition;
                            Vector3 w = predictedPosition - Player.ServerPosition;
                            double c1 = Vector3.Dot(w, v);
                            double c2 = Vector3.Dot(v, v);
                            double b = c1 / c2;
                            Vector3 pb = Player.ServerPosition + ((float)b * v);
                            float length = Vector3.Distance(predictedPosition, pb);
                            if (length < (R.Width + 150 + enemy.BoundingRadius / 2) && Player.Distance(predictedPosition) < Player.Distance(target.ServerPosition))
                                cast = false;
                        }
                        if (cast)
                            Program.CastSpell(R, target);
                    }
                }
            }
        }

        private void LogicQ()
        {
            if (Orbwalker.GetTarget() == null)
                return;
                var target = Orbwalker.GetTarget();
                if (GetQStacks() >= Config.Item("comboQ").GetValue<Slider>().Value && target.IsValid && target is Obj_AI_Hero)
                {
                    if (Program.Combo && (Player.Mana > RMANA + QMANA || target.Health <  5 * Player.GetAutoAttackDamage(Player)))
                        Q.Cast();
                    else if (Program.Farm && (Player.Mana > RMANA + QMANA + WMANA) && Config.Item("harasQ").GetValue<bool>())
                        Q.Cast();
                }
            }

        private int GetQStacks()
        {
            foreach (var buff in Player.Buffs)
            {
                if (buff.Name == "asheqcastready")
                    return buff.Count;
                else if (buff.Name == "AsheQ")
                    return buff.Count;
            }
            return 0;
        }

        private void LogicW()
        {

            var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
            if (ObjectManager.Player.CountEnemiesInRange(700) > 0)
                t = TargetSelector.GetTarget(700, TargetSelector.DamageType.Physical);
            if (t.IsValidTarget())
            {
                var poutput = W.GetPrediction(t);
                var col = poutput.CollisionObjects.Count(ColObj => ColObj.IsEnemy && ColObj.IsMinion && !ColObj.IsDead);
                if (t.IsDead || col > 0 || t.Path.Count() > 1 || (int)poutput.Hitchance < 5)
                    return;

                var wDmg = W.GetDamage(t);
                if (wDmg > t.Health)
                {
                    W.Cast(poutput.CastPosition);
                }
                else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + WMANA)
                    W.Cast(poutput.CastPosition);
                else if (Program.Farm && Config.Item("haras" + t.BaseSkinName).GetValue<bool>() && !ObjectManager.Player.UnderTurret(true) && ObjectManager.Player.Mana > RMANA + WMANA + QMANA + WMANA)
                    W.Cast(poutput.CastPosition);
                else if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo || Program.Farm) && ObjectManager.Player.Mana > RMANA + WMANA)
                {
                    foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(W.Range) && !Program.CanMove(enemy)))
                        W.Cast(enemy, true);
                }
            }
        }

        private void SetMana()
        {
            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;

            if (!R.IsReady())
                RMANA = WMANA - Player.PARRegenRate * W.Instance.Cooldown;
            else
                RMANA = R.Instance.ManaCost; ;

            if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.2)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
            }
        }

        private void LoadMenuOKTW()
        {
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu(Player.ChampionName).SubMenu("Haras W").AddItem(new MenuItem("haras" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));

            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu(Player.ChampionName).SubMenu("GapCloser R").AddItem(new MenuItem("GapCloser" + enemy.BaseSkinName, enemy.BaseSkinName,true).SetValue(false));

            Config.SubMenu(Player.ChampionName).SubMenu("Q Config").AddItem(new MenuItem("comboQ", "Q count").SetValue(new Slider(5, 5, 0)));
            Config.SubMenu(Player.ChampionName).SubMenu("Q Config").AddItem(new MenuItem("harasQ", "Haras Q").SetValue(true));

            Config.SubMenu(Player.ChampionName).AddItem(new MenuItem("autoE", "Auto E").SetValue(true));

            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw only ready spells").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("wRange", "W range").SetValue(false));

            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("autoR", "Auto R").SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("autoRaoe", "Auto R aoe").SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("autoRinter", "Auto R OnPossibleToInterrupt").SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("useR", "Semi-manual cast R key").SetValue(new KeyBind('t', KeyBindType.Press))); //32 == space
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu(Player.ChampionName).SubMenu("R Config").SubMenu("GapCloser R").AddItem(new MenuItem("GapCloser" + enemy.BaseSkinName, enemy.BaseSkinName, true).SetValue(false));
        }
    }
}
