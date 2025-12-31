using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Optimization;

// 封裝 Weibull 擬合的結果
public class WeibullFitResult
{
    public double Location { get; } // 位置 μ (mu)
    public double Scale { get; }    // 尺度 σ (sigma)
    public double Shape { get; }    // 形狀 ξ (xi)

    public WeibullFitResult(double location, double scale, double shape)
    {
        Location = location;
        Scale = scale;
        Shape = shape;
    }
}

public static class WeibullFitter
{
    // 主要的擬合方法
    public static WeibullFitResult Fit(List<double> samples)
    {
        if (samples == null || samples.Count < 3)
        {
            throw new ArgumentException("需要至少3個樣本點才能進行三參數 Weibull 擬合。");
        }

        // 定義負對數概似函數 (Negative Log-Likelihood)
        Func<MathNet.Numerics.LinearAlgebra.Vector<double>, double> objectiveFunction = p =>
        {
            double mu = p[0];
            double sigma = p[1];
            double xi = p[2];

            if (sigma <= 0 || xi <= 0)
                return double.PositiveInfinity;

            double logLikelihood = 0;
            foreach (var x in samples)
            {
                if (x <= mu)
                    return double.PositiveInfinity;

                // 三參數 Weibull 的 log PDF
                double z = (x - mu) / sigma;
                logLikelihood += Math.Log(xi) - Math.Log(sigma) + (xi - 1) * Math.Log(z) - Math.Pow(z, xi);
            }
            return -logLikelihood;
        };

        double initialLocation = samples.Min() * 0.9;
        double initialScale = samples.Average();
        double initialShape = 1.0;

        var initialGuess = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(new[] {
            initialLocation, initialScale, initialShape
        });

        var solver = new NelderMeadSimplex(1e-8, 10000);
        var result = solver.FindMinimum(ObjectiveFunction.Value(objectiveFunction), initialGuess);

        double fittedLocation = result.MinimizingPoint[0];
        double fittedScale = result.MinimizingPoint[1];
        double fittedShape = result.MinimizingPoint[2];

        return new WeibullFitResult(fittedLocation, fittedScale, fittedShape);
    }
}