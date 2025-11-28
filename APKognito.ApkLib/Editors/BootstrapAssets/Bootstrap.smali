.class public LBOOTSTRAP/PACKAGE/Bootstrap;
.super Landroid/app/Activity;
.source "Bootstrap.java"


# static fields
.field private static FRIENDLY_NAME:Ljava/lang/String; = null

.field private static REPORT_CRASHES:Ljava/lang/String; = null

.field private static final TAG:Ljava/lang/String; = "KognitoBootstrapper"

.field private static TARGET:Ljava/lang/String;


# direct methods
.method static constructor <clinit>()V
    .registers 1

    .line 33
    const-string v0, "{FRIENDLY_APP_NAME}"

    sput-object v0, LBOOTSTRAP/PACKAGE/Bootstrap;->FRIENDLY_NAME:Ljava/lang/String;

    .line 34
    const-string v0, "{ENABLE_CRASH_REPORTING}"

    sput-object v0, LBOOTSTRAP/PACKAGE/Bootstrap;->REPORT_CRASHES:Ljava/lang/String;

    .line 35
    const-string v0, "{BOOTSTRAP_TARGET_ACTIVITY}"

    sput-object v0, LBOOTSTRAP/PACKAGE/Bootstrap;->TARGET:Ljava/lang/String;

    return-void
.end method

.method public constructor <init>()V
    .registers 1

    .line 31
    invoke-direct {p0}, Landroid/app/Activity;-><init>()V

    return-void
.end method


# virtual methods
.method public createViewComponents(Ljava/lang/Throwable;)V
    .registers 12

    .line 82
    invoke-virtual {p1}, Ljava/lang/Throwable;->getMessage()Ljava/lang/String;

    move-result-object v0

    if-eqz v0, :cond_b

    .line 83
    invoke-virtual {p1}, Ljava/lang/Throwable;->getMessage()Ljava/lang/String;

    move-result-object v0

    goto :goto_d

    .line 84
    :cond_b
    const-string v0, "No message provided."

    .line 86
    :goto_d
    invoke-static {p1}, Landroid/util/Log;->getStackTraceString(Ljava/lang/Throwable;)Ljava/lang/String;

    move-result-object p1

    .line 87
    invoke-virtual {p0}, LBOOTSTRAP/PACKAGE/Bootstrap;->getResources()Landroid/content/res/Resources;

    move-result-object v1

    invoke-virtual {v1}, Landroid/content/res/Resources;->getDisplayMetrics()Landroid/util/DisplayMetrics;

    move-result-object v1

    iget v1, v1, Landroid/util/DisplayMetrics;->density:F

    const/high16 v2, 0x41800000    # 16.0f

    mul-float v1, v1, v2

    float-to-int v1, v1

    .line 89
    new-instance v3, Landroid/widget/LinearLayout;

    invoke-direct {v3, p0}, Landroid/widget/LinearLayout;-><init>(Landroid/content/Context;)V

    .line 90
    const/4 v4, 0x1

    invoke-virtual {v3, v4}, Landroid/widget/LinearLayout;->setOrientation(I)V

    .line 91
    const v5, -0xbbbbbc

    invoke-virtual {v3, v5}, Landroid/widget/LinearLayout;->setBackgroundColor(I)V

    .line 92
    invoke-virtual {v3, v1, v1, v1, v1}, Landroid/widget/LinearLayout;->setPadding(IIII)V

    .line 95
    new-instance v5, Landroid/widget/TextView;

    invoke-direct {v5, p0}, Landroid/widget/TextView;-><init>(Landroid/content/Context;)V

    .line 96
    const-string v6, "Bootstrap Error"

    invoke-virtual {v5, v6}, Landroid/widget/TextView;->setText(Ljava/lang/CharSequence;)V

    .line 97
    const/high16 v6, 0x41a00000    # 20.0f

    invoke-virtual {v5, v6}, Landroid/widget/TextView;->setTextSize(F)V

    .line 98
    const/4 v6, -0x1

    invoke-virtual {v5, v6}, Landroid/widget/TextView;->setTextColor(I)V

    .line 99
    invoke-virtual {v3, v5}, Landroid/widget/LinearLayout;->addView(Landroid/view/View;)V

    .line 102
    new-instance v5, Landroid/widget/TextView;

    invoke-direct {v5, p0}, Landroid/widget/TextView;-><init>(Landroid/content/Context;)V

    .line 103
    div-int/lit8 v7, v1, 0x2

    invoke-virtual {v5, v7}, Landroid/widget/TextView;->setHeight(I)V

    .line 104
    invoke-virtual {v3, v5}, Landroid/widget/LinearLayout;->addView(Landroid/view/View;)V

    .line 107
    new-instance v5, Landroid/widget/TextView;

    invoke-direct {v5, p0}, Landroid/widget/TextView;-><init>(Landroid/content/Context;)V

    .line 108
    invoke-virtual {v5, v0}, Landroid/widget/TextView;->setText(Ljava/lang/CharSequence;)V

    .line 109
    invoke-virtual {v5, v2}, Landroid/widget/TextView;->setTextSize(F)V

    .line 110
    const/high16 v0, -0x10000

    invoke-virtual {v5, v0}, Landroid/widget/TextView;->setTextColor(I)V

    .line 111
    invoke-virtual {v3, v5}, Landroid/widget/LinearLayout;->addView(Landroid/view/View;)V

    .line 114
    new-instance v0, Landroid/widget/TextView;

    invoke-direct {v0, p0}, Landroid/widget/TextView;-><init>(Landroid/content/Context;)V

    .line 115
    invoke-virtual {v0, v1}, Landroid/widget/TextView;->setHeight(I)V

    .line 116
    invoke-virtual {v3, v0}, Landroid/widget/LinearLayout;->addView(Landroid/view/View;)V

    .line 119
    new-instance v0, Landroid/widget/TextView;

    invoke-direct {v0, p0}, Landroid/widget/TextView;-><init>(Landroid/content/Context;)V

    .line 120
    const-string v1, "Stack Trace Details"

    invoke-virtual {v0, v1}, Landroid/widget/TextView;->setText(Ljava/lang/CharSequence;)V

    .line 121
    const/high16 v1, 0x41600000    # 14.0f

    invoke-virtual {v0, v1}, Landroid/widget/TextView;->setTextSize(F)V

    .line 122
    invoke-virtual {v0, v6}, Landroid/widget/TextView;->setTextColor(I)V

    .line 123
    invoke-virtual {v3, v0}, Landroid/widget/LinearLayout;->addView(Landroid/view/View;)V

    .line 126
    new-instance v0, Landroid/widget/ScrollView;

    invoke-direct {v0, p0}, Landroid/widget/ScrollView;-><init>(Landroid/content/Context;)V

    .line 127
    new-instance v1, Landroid/widget/EditText;

    invoke-direct {v1, p0}, Landroid/widget/EditText;-><init>(Landroid/content/Context;)V

    .line 128
    const/4 v5, 0x4

    new-array v5, v5, [Ljava/lang/Object;

    sget-object v8, LBOOTSTRAP/PACKAGE/Bootstrap;->TARGET:Ljava/lang/String;

    const/4 v9, 0x0

    aput-object v8, v5, v9

    .line 129
    invoke-virtual {p0}, Ljava/lang/Object;->getClass()Ljava/lang/Class;

    move-result-object v8

    invoke-virtual {v8}, Ljava/lang/Class;->getName()Ljava/lang/String;

    move-result-object v8

    aput-object v8, v5, v4

    sget-object v4, LBOOTSTRAP/PACKAGE/Bootstrap;->FRIENDLY_NAME:Ljava/lang/String;

    const/4 v8, 0x2

    aput-object v4, v5, v8

    const/4 v4, 0x3

    aput-object p1, v5, v4

    .line 128
    const-string p1, "Attempted activity path: %s\nFrom activity: %s\nFor app: %s\n\n%s"

    invoke-static {p1, v5}, Ljava/lang/String;->format(Ljava/lang/String;[Ljava/lang/Object;)Ljava/lang/String;

    move-result-object p1

    invoke-virtual {v1, p1}, Landroid/widget/EditText;->setText(Ljava/lang/CharSequence;)V

    .line 130
    invoke-virtual {v1, v9}, Landroid/widget/EditText;->setSingleLine(Z)V

    .line 131
    invoke-virtual {v1, v9}, Landroid/widget/EditText;->setFocusable(Z)V

    .line 132
    const p1, 0x999999

    invoke-virtual {v1, p1}, Landroid/widget/EditText;->setBackgroundColor(I)V

    .line 133
    invoke-virtual {v1, v6}, Landroid/widget/EditText;->setTextColor(I)V

    .line 134
    const/4 p1, 0x5

    invoke-virtual {v1, p1}, Landroid/widget/EditText;->setMinLines(I)V

    .line 135
    invoke-virtual {v1, v2}, Landroid/widget/EditText;->setTextSize(F)V

    .line 136
    const p1, 0x24001

    invoke-virtual {v1, p1}, Landroid/widget/EditText;->setInputType(I)V

    .line 140
    invoke-virtual {v1, v7, v7, v7, v7}, Landroid/widget/EditText;->setPadding(IIII)V

    .line 142
    new-instance p1, Landroid/view/ViewGroup$LayoutParams;

    const/4 v2, -0x2

    invoke-direct {p1, v6, v2}, Landroid/view/ViewGroup$LayoutParams;-><init>(II)V

    invoke-virtual {v0, v1, p1}, Landroid/widget/ScrollView;->addView(Landroid/view/View;Landroid/view/ViewGroup$LayoutParams;)V

    .line 143
    invoke-virtual {v3, v0}, Landroid/widget/LinearLayout;->addView(Landroid/view/View;)V

    .line 145
    invoke-virtual {p0, v3}, LBOOTSTRAP/PACKAGE/Bootstrap;->setContentView(Landroid/view/View;)V

    .line 146
    return-void
.end method

.method protected onCreate(Landroid/os/Bundle;)V
    .registers 5

    .line 43
    invoke-super {p0, p1}, Landroid/app/Activity;->onCreate(Landroid/os/Bundle;)V

    .line 45
    new-instance p1, Ljava/lang/StringBuilder;

    invoke-direct {p1}, Ljava/lang/StringBuilder;-><init>()V

    const-string v0, "Bootstrap starting target: "

    invoke-virtual {p1, v0}, Ljava/lang/StringBuilder;->append(Ljava/lang/String;)Ljava/lang/StringBuilder;

    move-result-object p1

    sget-object v0, LBOOTSTRAP/PACKAGE/Bootstrap;->TARGET:Ljava/lang/String;

    invoke-virtual {p1, v0}, Ljava/lang/StringBuilder;->append(Ljava/lang/String;)Ljava/lang/StringBuilder;

    move-result-object p1

    invoke-virtual {p1}, Ljava/lang/StringBuilder;->toString()Ljava/lang/String;

    move-result-object p1

    const-string v0, "KognitoBootstrapper"

    invoke-static {v0, p1}, Landroid/util/Log;->i(Ljava/lang/String;Ljava/lang/String;)I

    .line 48
    :try_start_1d
    sget-object p1, LBOOTSTRAP/PACKAGE/Bootstrap;->TARGET:Ljava/lang/String;

    const/16 v1, 0x2e

    invoke-virtual {p1, v1}, Ljava/lang/String;->lastIndexOf(I)I

    move-result p1

    .line 49
    if-ltz p1, :cond_59

    .line 53
    sget-object v1, LBOOTSTRAP/PACKAGE/Bootstrap;->TARGET:Ljava/lang/String;

    const/4 v2, 0x0

    invoke-virtual {v1, v2, p1}, Ljava/lang/String;->substring(II)Ljava/lang/String;

    .line 55
    new-instance p1, Landroid/content/Intent;

    invoke-direct {p1}, Landroid/content/Intent;-><init>()V

    .line 58
    sget-object v1, LBOOTSTRAP/PACKAGE/Bootstrap;->TARGET:Ljava/lang/String;

    invoke-virtual {p1, p0, v1}, Landroid/content/Intent;->setClassName(Landroid/content/Context;Ljava/lang/String;)Landroid/content/Intent;

    .line 60
    invoke-virtual {p0}, LBOOTSTRAP/PACKAGE/Bootstrap;->getIntent()Landroid/content/Intent;

    move-result-object v1

    if-eqz v1, :cond_52

    invoke-virtual {p0}, LBOOTSTRAP/PACKAGE/Bootstrap;->getIntent()Landroid/content/Intent;

    move-result-object v1

    invoke-virtual {v1}, Landroid/content/Intent;->getExtras()Landroid/os/Bundle;

    move-result-object v1

    if-eqz v1, :cond_52

    .line 61
    invoke-virtual {p0}, LBOOTSTRAP/PACKAGE/Bootstrap;->getIntent()Landroid/content/Intent;

    move-result-object v1

    invoke-virtual {v1}, Landroid/content/Intent;->getExtras()Landroid/os/Bundle;

    move-result-object v1

    invoke-virtual {p1, v1}, Landroid/content/Intent;->putExtras(Landroid/os/Bundle;)Landroid/content/Intent;

    .line 64
    :cond_52
    invoke-virtual {p0, p1}, LBOOTSTRAP/PACKAGE/Bootstrap;->startActivity(Landroid/content/Intent;)V

    .line 65
    invoke-virtual {p0}, LBOOTSTRAP/PACKAGE/Bootstrap;->finish()V

    .line 78
    goto :goto_8e

    .line 50
    :cond_59
    new-instance p1, Ljava/lang/IllegalArgumentException;

    const-string v1, "TARGET must be full-qualified class name"

    invoke-direct {p1, v1}, Ljava/lang/IllegalArgumentException;-><init>(Ljava/lang/String;)V

    throw p1
    :try_end_61
    .catchall {:try_start_1d .. :try_end_61} :catchall_61

    .line 66
    :catchall_61
    move-exception p1

    .line 67
    new-instance v1, Ljava/lang/StringBuilder;

    invoke-direct {v1}, Ljava/lang/StringBuilder;-><init>()V

    const-string v2, "Failed to start target activity: "

    invoke-virtual {v1, v2}, Ljava/lang/StringBuilder;->append(Ljava/lang/String;)Ljava/lang/StringBuilder;

    move-result-object v1

    sget-object v2, LBOOTSTRAP/PACKAGE/Bootstrap;->TARGET:Ljava/lang/String;

    invoke-virtual {v1, v2}, Ljava/lang/StringBuilder;->append(Ljava/lang/String;)Ljava/lang/StringBuilder;

    move-result-object v1

    invoke-virtual {v1}, Ljava/lang/StringBuilder;->toString()Ljava/lang/String;

    move-result-object v1

    invoke-static {v0, v1, p1}, Landroid/util/Log;->e(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Throwable;)I

    .line 69
    sget-object v1, LBOOTSTRAP/PACKAGE/Bootstrap;->REPORT_CRASHES:Ljava/lang/String;

    const-string v2, "true"

    if-ne v1, v2, :cond_86

    .line 71
    :try_start_80
    invoke-virtual {p0, p1}, LBOOTSTRAP/PACKAGE/Bootstrap;->createViewComponents(Ljava/lang/Throwable;)V
    :try_end_83
    .catchall {:try_start_80 .. :try_end_83} :catchall_84

    .line 73
    :goto_83
    goto :goto_8e

    .line 72
    :catchall_84
    move-exception p1

    goto :goto_83

    .line 75
    :cond_86
    const-string p1, "Crash reporting disabled. Exiting."

    invoke-static {v0, p1}, Landroid/util/Log;->i(Ljava/lang/String;Ljava/lang/String;)I

    .line 76
    invoke-virtual {p0}, LBOOTSTRAP/PACKAGE/Bootstrap;->finish()V

    .line 79
    :goto_8e
    return-void
.end method
