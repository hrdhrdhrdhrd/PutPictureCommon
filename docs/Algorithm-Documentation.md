# 算法文档

## 目录
- [颜色匹配算法](#颜色匹配算法)
- [透明化处理算法](#透明化处理算法)
- [边缘处理算法](#边缘处理算法)
- [形态学处理算法](#形态学处理算法)
- [性能优化技术](#性能优化技术)
- [算法参数调优](#算法参数调优)

## 颜色匹配算法

### 多色彩空间匹配算法

PutPicture 使用多色彩空间融合的颜色匹配算法，结合 RGB、HSV 和 Lab 色彩空间的优势，实现更准确的颜色匹配。

#### 算法流程

```
输入: 像素RGB值(r,g,b), 目标颜色, 容差
    ↓
1. RGB欧几里得距离预筛选
    ↓
2. 转换到HSV色彩空间
    ↓
3. 计算HSV匹配分数
    ↓
4. 转换到Lab色彩空间
    ↓
5. 计算Lab感知距离
    ↓
6. 综合评分决策
    ↓
输出: 是否匹配 (true/false)
```

#### 1. RGB 欧几里得距离

```csharp
double rgbDistance = Math.Sqrt(
    Math.Pow(r - targetColor.R, 2) + 
    Math.Pow(g - targetColor.G, 2) + 
    Math.Pow(b - targetColor.B, 2)
);

// 快速预筛选，排除明显不匹配的颜色
if (rgbDistance > tolerance * 2.5) return false;
```

**特点**:
- 计算简单，速度快
- 适合快速预筛选
- 对亮度变化敏感

#### 2. HSV 色彩空间匹配

```csharp
// RGB转HSV
var pixelHsv = RgbToHsv(r, g, b);
var targetHsv = RgbToHsv(targetColor.R, targetColor.G, targetColor.B);

// 色调差异（考虑环形特性）
double hueDiff = Math.Min(
    Math.Abs(pixelHsv.H - targetHsv.H),
    360 - Math.Abs(pixelHsv.H - targetHsv.H)
);

// 饱和度和明度差异
double satDiff = Math.Abs(pixelHsv.S - targetHsv.S);
double valDiff = Math.Abs(pixelHsv.V - targetHsv.V);

// HSV 综合评分
double hsvScore = (hueDiff / 180.0) * 0.4 + (satDiff / 100.0) * 0.3 + (valDiff / 100.0) * 0.3;
```

**特点**:
- 符合人眼对颜色的感知
- 色调(H)、饱和度(S)、明度(V)分离
- 对色调变化敏感

#### 3. Lab 色彩空间匹配

```csharp
// RGB转Lab
var pixelLab = RgbToLab(r, g, b);
var targetLab = RgbToLab(targetColor.R, targetColor.G, targetColor.B);

// Lab感知距离
double labDistance = Math.Sqrt(
    Math.Pow(pixelLab.L - targetLab.L, 2) + 
    Math.Pow(pixelLab.A - targetLab.A, 2) + 
    Math.Pow(pixelLab.B - targetLab.B, 2)
);
```

**特点**:
- 感知均匀色彩空间
- 距离计算符合人眼感知差异
- 对细微颜色差异敏感

#### 4. 综合决策算法

```csharp
// 特殊处理：低饱和度颜色（灰色、白色等）
if (targetHsv.S < 0.1)
{
    return valDiff < tolerance * 0.8; // 主要看明度
}

// 综合判断
return (rgbDistance < tolerance * 1.5) && 
       (hsvScore < toleranceNormalized * 0.6) && 
       (labDistance < tolerance * 1.2);
```

**权重分配**:
- RGB距离: 40%
- HSV评分: 30%
- Lab距离: 30%

### 自适应容差算法

根据图像的颜色复杂度自动调整容差值。

#### 算法原理

```csharp
// 1. 采样分析（每隔10个像素）
var colorVariances = new List<double>();
for (int y = 0; y < height; y += 10)
{
    for (int x = 0; x < width; x += 10)
    {
        // 计算与目标颜色的距离
        double distance = CalculateColorDistance(pixel, targetColor);
        colorVariances.Add(distance);
    }
}

// 2. 计算颜色方差
double avgDistance = colorVariances.Average();
double variance = colorVariances.Select(d => Math.Pow(d - avgDistance, 2)).Average();
double stdDev = Math.Sqrt(variance);

// 3. 自适应因子计算
double adaptiveFactor = Math.Max(0.5, Math.Min(2.0, stdDev / 50.0));
int adaptiveTolerance = (int)(baseTolerance * adaptiveFactor);
```

**适应性规则**:
- 高方差（复杂图像）→ 增加容差
- 低方差（简单图像）→ 减少容差
- 容差范围限制在 [10, 200]

## 透明化处理算法

### 基础透明化算法

```csharp
// 高性能内存操作
unsafe
{
    byte* srcPtr = (byte*)bitmapData.Scan0;
    byte* dstPtr = (byte*)resultData.Scan0;
    
    Parallel.For(0, height, y =>
    {
        byte* srcRow = srcPtr + y * stride;
        byte* dstRow = dstPtr + y * stride;
        
        for (int x = 0; x < width; x++)
        {
            int pixelIndex = x * 4;
            
            byte b = srcRow[pixelIndex];
            byte g = srcRow[pixelIndex + 1];
            byte r = srcRow[pixelIndex + 2];
            byte a = srcRow[pixelIndex + 3];
            
            bool shouldMakeTransparent = IsColorMatch(r, g, b, targetColor, tolerance);
            
            dstRow[pixelIndex] = b;
            dstRow[pixelIndex + 1] = g;
            dstRow[pixelIndex + 2] = r;
            dstRow[pixelIndex + 3] = shouldMakeTransparent ? (byte)0 : a;
        }
    });
}
```

### 渐变透明化算法

为边缘区域提供平滑的透明度过渡。

```csharp
// 计算颜色匹配分数 (0-1)
double matchScore = CalculateColorMatchScore(r, g, b, targetColor, tolerance);

// 分段透明度计算
byte alpha;
if (matchScore <= 0.3)
{
    alpha = 0;                              // 高度匹配 - 完全透明
}
else if (matchScore >= 0.7)
{
    alpha = originalAlpha;                  // 低匹配度 - 保持不透明
}
else
{
    // 中等匹配度 - 渐变透明
    double alphaFactor = (matchScore - 0.3) / 0.4;
    alpha = (byte)(originalAlpha * alphaFactor);
}
```

**渐变区间**:
- [0, 0.3]: 完全透明
- [0.3, 0.7]: 渐变透明
- [0.7, 1.0]: 保持不透明

## 边缘处理算法

### 边缘检测算法

使用 Sobel 算子检测图像边缘。

```csharp
// Sobel 算子
int[,] sobelX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
int[,] sobelY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

// 计算梯度
int gx = 0, gy = 0;
for (int dy = -1; dy <= 1; dy++)
{
    for (int dx = -1; dx <= 1; dx++)
    {
        int alpha = GetPixelAlpha(x + dx, y + dy);
        gx += alpha * sobelX[dy + 1, dx + 1];
        gy += alpha * sobelY[dy + 1, dx + 1];
    }
}

// 梯度幅值
int magnitude = (int)Math.Sqrt(gx * gx + gy * gy);
```

### 抗锯齿算法

对边缘像素进行平滑处理。

```csharp
// 3x3 邻域平均
int totalR = 0, totalG = 0, totalB = 0, totalA = 0;
int count = 0;

for (int dy = -1; dy <= 1; dy++)
{
    for (int dx = -1; dx <= 1; dx++)
    {
        if (IsValidPixel(x + dx, y + dy))
        {
            var neighbor = GetPixel(x + dx, y + dy);
            totalR += neighbor.R;
            totalG += neighbor.G;
            totalB += neighbor.B;
            totalA += neighbor.A;
            count++;
        }
    }
}

// 平均值作为抗锯齿结果
if (count > 0)
{
    resultPixel.R = (byte)(totalR / count);
    resultPixel.G = (byte)(totalG / count);
    resultPixel.B = (byte)(totalB / count);
    resultPixel.A = (byte)(totalA / count);
}
```

### 边缘羽化算法

为透明边缘创建柔和过渡。

```csharp
// 寻找最近的不透明像素
int nearestR = 0, nearestG = 0, nearestB = 0;
int minDistance = int.MaxValue;
bool found = false;

for (int dy = -2; dy <= 2; dy++)
{
    for (int dx = -2; dx <= 2; dx++)
    {
        if (dx == 0 && dy == 0) continue;
        
        var neighbor = GetPixel(x + dx, y + dy);
        if (neighbor.A > 128)
        {
            int distance = Math.Abs(dx) + Math.Abs(dy);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestR = neighbor.R;
                nearestG = neighbor.G;
                nearestB = neighbor.B;
                found = true;
            }
        }
    }
}

// 根据距离计算羽化透明度
if (found)
{
    byte featherAlpha = (byte)Math.Max(0, 64 - minDistance * 20);
    resultPixel = new Color(nearestR, nearestG, nearestB, featherAlpha);
}
```

## 形态学处理算法

### 腐蚀算法 (Erosion)

去除小的噪点和细节。

```csharp
// 检查邻域内是否有透明像素
bool hasTransparentNeighbor = false;
for (int dy = -radius; dy <= radius; dy++)
{
    for (int dx = -radius; dx <= radius; dx++)
    {
        var neighbor = GetPixel(x + dx, y + dy);
        if (neighbor.A < 128)
        {
            hasTransparentNeighbor = true;
            break;
        }
    }
}

// 如果邻域有透明像素，当前像素变透明
if (hasTransparentNeighbor)
{
    resultPixel.A = 0;
}
```

### 膨胀算法 (Dilation)

填充小的孔洞。

```csharp
// 寻找邻域内的不透明像素
int maxAlpha = 0;
int sumR = 0, sumG = 0, sumB = 0, count = 0;

for (int dy = -radius; dy <= radius; dy++)
{
    for (int dx = -radius; dx <= radius; dx++)
    {
        var neighbor = GetPixel(x + dx, y + dy);
        if (neighbor.A > maxAlpha)
        {
            maxAlpha = neighbor.A;
        }
        if (neighbor.A > 128)
        {
            sumR += neighbor.R;
            sumG += neighbor.G;
            sumB += neighbor.B;
            count++;
        }
    }
}

// 使用邻域平均颜色和最大透明度
if (count > 0)
{
    resultPixel.R = (byte)(sumR / count);
    resultPixel.G = (byte)(sumG / count);
    resultPixel.B = (byte)(sumB / count);
    resultPixel.A = (byte)Math.Min(255, maxAlpha);
}
```

### 开运算和闭运算

```csharp
// 开运算 = 腐蚀 + 膨胀（去除噪点）
var eroded = ApplyErosion(bitmap, kernelSize);
var opened = ApplyDilation(eroded, kernelSize);

// 闭运算 = 膨胀 + 腐蚀（填充孔洞）
var dilated = ApplyDilation(opened, kernelSize);
var closed = ApplyErosion(dilated, kernelSize);
```

## 性能优化技术

### 1. 内存访问优化

```csharp
// 使用 unsafe 指针直接访问内存
unsafe
{
    byte* ptr = (byte*)bitmapData.Scan0;
    int stride = bitmapData.Stride;
    
    // 按行访问，利用内存局部性
    for (int y = 0; y < height; y++)
    {
        byte* row = ptr + y * stride;
        for (int x = 0; x < width; x++)
        {
            // 直接内存操作，避免GetPixel/SetPixel
            int index = x * 4;
            byte b = row[index];
            byte g = row[index + 1];
            byte r = row[index + 2];
            byte a = row[index + 3];
        }
    }
}
```

### 2. 并行处理优化

```csharp
// 行级并行处理
Parallel.For(0, height, new ParallelOptions 
{ 
    MaxDegreeOfParallelism = Environment.ProcessorCount - 1 
}, y =>
{
    // 处理第 y 行
    ProcessRow(y);
});

// 避免false sharing，每个线程处理连续的行
```

### 3. 算法优化

```csharp
// 预计算目标颜色的HSV和Lab值
var targetHsv = RgbToHsv(targetColor.R, targetColor.G, targetColor.B);
var targetLab = RgbToLab(targetColor.R, targetColor.G, targetColor.B);

// 在循环中重用预计算的值
for (int i = 0; i < pixelCount; i++)
{
    bool match = IsColorMatch(r, g, b, targetColor, targetHsv, targetLab, tolerance);
}
```

### 4. 内存管理优化

```csharp
// 及时释放大对象
using (var bitmap = new Bitmap(width, height))
{
    // 处理逻辑
} // 自动释放

// 对于大批量处理，定期强制垃圾回收
if (processedCount % 100 == 0)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
}
```

## 算法参数调优

### 容差参数 (Tolerance)

| 场景 | 推荐值 | 说明 |
|------|--------|------|
| 纯色背景 | 20-40 | 背景颜色单一，容差可以较小 |
| 渐变背景 | 50-80 | 背景有渐变，需要较大容差 |
| 复杂背景 | 60-100 | 背景复杂，需要大容差 |
| 高精度要求 | 10-30 | 要求精确匹配，使用小容差 |

### 去噪核大小 (DespeckleSize)

| 噪点程度 | 推荐值 | 效果 |
|----------|--------|------|
| 无噪点 | 1 | 最小处理，保持细节 |
| 轻微噪点 | 3 | 平衡去噪和细节保持 |
| 中等噪点 | 5 | 较强去噪，可能损失细节 |
| 严重噪点 | 7 | 最强去噪，明显损失细节 |

### 并行度设置

```csharp
// 根据图片大小和CPU核心数动态调整
int optimalParallelism;
if (totalPixels < 1000000) // 小图片
{
    optimalParallelism = Math.Min(4, Environment.ProcessorCount);
}
else if (totalPixels < 10000000) // 中等图片
{
    optimalParallelism = Environment.ProcessorCount - 1;
}
else // 大图片
{
    optimalParallelism = Environment.ProcessorCount;
}
```

### 功能开关优化建议

| 功能 | 小图片 | 大图片 | 批量处理 | 高质量要求 |
|------|--------|--------|----------|------------|
| EdgeSmoothing | ✓ | ✗ | ✗ | ✓ |
| AdaptiveTolerance | ✓ | ✓ | ✓ | ✓ |
| Morphology | ✓ | ✗ | ✗ | ✓ |
| GradientAlpha | ✓ | ✓ | ✗ | ✓ |

### 性能基准测试

```csharp
// 测试不同参数组合的性能
public void BenchmarkParameters()
{
    var testImage = new Bitmap(1000, 1000);
    var configurations = new[]
    {
        new { Tolerance = 30, EdgeSmoothing = false, Morphology = false },
        new { Tolerance = 50, EdgeSmoothing = true, Morphology = false },
        new { Tolerance = 50, EdgeSmoothing = true, Morphology = true },
    };
    
    foreach (var config in configurations)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // 执行处理
        var result = ProcessWithConfig(testImage, config);
        
        stopwatch.Stop();
        Console.WriteLine($"配置: {config}, 耗时: {stopwatch.ElapsedMilliseconds}ms");
    }
}
```

通过理解这些算法原理和优化技术，开发者可以根据具体需求调整参数，在处理质量和性能之间找到最佳平衡点。