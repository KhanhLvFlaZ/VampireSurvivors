import matplotlib.pyplot as plt
import numpy as np

labels = ["RL Policy", "FSM/BT"]
survival = [580, 480]
kills = [215, 156]
latency = [8.5, 0.2]

def save_bar(values, title, ylabel, fname, invert=False, ylim=None):
    x = np.arange(len(labels))
    width = 0.6
    fig, ax = plt.subplots(figsize=(4, 3))
    bars = ax.bar(x, values, width, color=["#4a90e2", "#7b8ba3"])
    ax.set_xticks(x, labels)
    ax.set_title(title)
    ax.set_ylabel(ylabel)
    ax.bar_label(bars, padding=3, fmt="%.1f")
    if invert:
        ax.invert_yaxis()
        if ylim:
            ax.set_ylim(ylim[1], ylim[0])
        else:
            ax.set_ylim(max(values) * 1.2, 0)
    elif ylim:
        ax.set_ylim(ylim)
    fig.tight_layout()
    fig.savefig(fname, dpi=200)
    plt.close(fig)

save_bar(survival, "Survival Time (s)", "Seconds", "survival.png", ylim=(0, 700))
save_bar(kills, "Kill Count", "Count", "kills.png", ylim=(0, 250))
save_bar(latency, "Inference Latency (ms)\n(lower is better)", "ms", "latency.png", invert=True)
print("Đã lưu: survival.png, kills.png, latency.png")
