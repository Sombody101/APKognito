.class public Lio/overrides/Bootstrap;
.super Landroid/app/Activity;
.source "Bootstrap.java"


# static fields
.field private static final TAG:Ljava/lang/String; = "Bootstrapper"


# direct methods
.method public constructor <init>()V
    .registers 1

    .line 8
    invoke-direct {p0}, Landroid/app/Activity;-><init>()V

    return-void
.end method


# virtual methods
.method protected onCreate(Landroid/os/Bundle;)V
    .registers 4

    .line 14
    invoke-super {p0, p1}, Landroid/app/Activity;->onCreate(Landroid/os/Bundle;)V

    .line 17
    :try_start_3
    const-string p1, "{BOOTSTRAP_TARGET_CLASS}"

    .line 18
    new-instance v0, Landroid/content/Intent;

    invoke-direct {v0}, Landroid/content/Intent;-><init>()V

    .line 19
    invoke-virtual {v0, p0, p1}, Landroid/content/Intent;->setClassName(Landroid/content/Context;Ljava/lang/String;)Landroid/content/Intent;

    .line 20
    invoke-virtual {p0}, Lio/overrides/Bootstrap;->getIntent()Landroid/content/Intent;

    move-result-object p1

    invoke-virtual {v0, p1}, Landroid/content/Intent;->putExtras(Landroid/content/Intent;)Landroid/content/Intent;

    .line 21
    invoke-virtual {p0, v0}, Lio/overrides/Bootstrap;->startActivity(Landroid/content/Intent;)V
    :try_end_17
    .catch Ljava/lang/Exception; {:try_start_3 .. :try_end_17} :catch_1a
    .catchall {:try_start_3 .. :try_end_17} :catchall_18

    goto :goto_22

    .line 26
    :catchall_18
    move-exception p1

    goto :goto_27

    .line 23
    :catch_1a
    move-exception p1

    .line 24
    :try_start_1b
    const-string v0, "Bootstrapper"

    const-string v1, "Failed to start original activity."

    invoke-static {v0, v1, p1}, Landroid/util/Log;->e(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Throwable;)I
    :try_end_22
    .catchall {:try_start_1b .. :try_end_22} :catchall_18

    .line 26
    :goto_22
    invoke-virtual {p0}, Lio/overrides/Bootstrap;->finish()V

    .line 27
    nop

    .line 28
    return-void

    .line 26
    :goto_27
    invoke-virtual {p0}, Lio/overrides/Bootstrap;->finish()V

    .line 27
    throw p1
.end method
