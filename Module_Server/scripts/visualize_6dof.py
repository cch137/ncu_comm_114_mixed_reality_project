import numpy as np
import matplotlib.pyplot as plt
import pandas as pd

def calculate_speeds(positions):
    """計算每個點的速度"""
    speeds = np.zeros(len(positions))
    for i in range(1, len(positions)):
        # 計算位置變化
        pos_speed = np.linalg.norm(positions[i] - positions[i-1])
        speeds[i] = pos_speed

    return speeds

def plot_2d_trajectory(csv_file):
    # 讀取CSV文件
    df = pd.read_csv(csv_file)

    # 提取位置數據（只取X和Z）
    positions = df[['Position_X', 'Position_Z']].values

    # 計算速度
    speeds = calculate_speeds(positions)

    # 創建圖形
    plt.figure(figsize=(10, 8))

    # 繪製軌跡
    scatter = plt.scatter(positions[:, 0], positions[:, 1],
                         c=speeds, cmap='viridis', s=50, alpha=0.6)

    # 連接點
    plt.plot(positions[:, 0], positions[:, 1],
            'gray', alpha=0.3, linewidth=1)

    # 添加顏色條
    cbar = plt.colorbar(scatter)
    cbar.set_label('Speed (normalized)')

    # 設置標籤
    plt.xlabel('X Position')
    plt.ylabel('Z Position')
    plt.title('2D Movement Trajectory (X-Z Plane)')

    # 添加網格
    plt.grid(True)

    # 設置等比例
    plt.axis('equal')

    # 保存圖片（高解析度）
    plt.savefig('2d_trajectory.png', dpi=300, bbox_inches='tight')
    plt.close()

def plot_rotation_analysis(csv_file):
    """分析並繪製旋轉數據"""
    # 讀取CSV文件
    df = pd.read_csv(csv_file)

    # 提取旋轉數據
    rotations = df[['Rotation_X', 'Rotation_Y', 'Rotation_Z']].values

    # 創建圖形
    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(12, 10))

    # 繪製旋轉角度隨時間變化
    time = np.arange(len(rotations))
    ax1.plot(time, rotations[:, 0], 'r-', label='X Rotation', linewidth=1.5)
    ax1.plot(time, rotations[:, 1], 'g-', label='Y Rotation', linewidth=1.5)
    ax1.plot(time, rotations[:, 2], 'b-', label='Z Rotation', linewidth=1.5)
    ax1.set_xlabel('Time Step')
    ax1.set_ylabel('Rotation Angle (degrees)')
    ax1.set_title('Rotation Angles Over Time')
    ax1.legend()
    ax1.grid(True)

    # 計算旋轉速度
    rotation_speeds = np.zeros(len(rotations))
    for i in range(1, len(rotations)):
        rotation_speeds[i] = np.linalg.norm(rotations[i] - rotations[i-1])

    # 繪製旋轉速度
    ax2.plot(time, rotation_speeds, 'k-', linewidth=1.5)
    ax2.set_xlabel('Time Step')
    ax2.set_ylabel('Rotation Speed (degrees/step)')
    ax2.set_title('Rotation Speed Over Time')
    ax2.grid(True)

    # 調整子圖之間的間距
    plt.tight_layout()

    # 保存圖片
    plt.savefig('rotation_analysis.png', dpi=300, bbox_inches='tight')
    plt.close()

if __name__ == "__main__":
    # csv_file = "UserPositionData(固定).csv"  # CSV文件路徑
    csv_file = "UserPositionData(走動).csv"  # CSV文件路徑
    plot_2d_trajectory(csv_file)
    plot_rotation_analysis(csv_file)