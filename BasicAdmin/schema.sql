create table {prefix}admins
(
    id         int auto_increment
        primary key,
    nick       varchar(255)                         not null,
    steamid64  bigint                               not null,
    immunity   int      default 0                   null,
    created_at datetime default current_timestamp() null,
    updated_at datetime default current_timestamp() null on update current_timestamp(),
    constraint steamid64
        unique (steamid64)
);

create table {prefix}flag_con
(
    id         int auto_increment
        primary key,
    flag_id    int                                  not null,
    group_id   int                                  null,
    admin_id   int                                  null,
    created_at datetime default current_timestamp() null,
    updated_at datetime default current_timestamp() null on update current_timestamp(),
    constraint flag_admin
        unique (flag_id, admin_id),
    constraint flag_group
        unique (flag_id, group_id)
);

create table {prefix}flags
(
    id         int auto_increment
        primary key,
    name       varchar(255)                         not null,
    value      varchar(255)                         not null,
    created_at datetime default current_timestamp() null,
    updated_at datetime default current_timestamp() null on update current_timestamp(),
    constraint value
        unique (value)
);

create table {prefix}group_admins
(
    id         int auto_increment
        primary key,
    group_id   int                                  not null,
    admin_id   int                                  not null,
    created_at datetime default current_timestamp() null,
    updated_at datetime default current_timestamp() null on update current_timestamp(),
    constraint group_admin
        unique (group_id, admin_id)
);

create table `{prefix}groups`
(
    id         int auto_increment
        primary key,
    name       varchar(255)                         not null,
    immunity   int      default 0                   null,
    created_at datetime default current_timestamp() null,
    updated_at datetime default current_timestamp() null on update current_timestamp(),
    constraint name
        unique (name)
);

create table {prefix}punishments
(
    id          int auto_increment
        primary key,
    server_id   int unsigned                         not null,
    admin_id    bigint unsigned                      not null,
    target      bigint unsigned                      not null comment 'steamid64 of target',
    target_name varchar(255)                         not null comment 'name of target',
    reason      varchar(1024)                        not null,
    length      int                                  not null,
    type        tinyint unsigned                     not null comment '0 - ban, 1 - gag, 2 - mute',
    expires_at  datetime default current_timestamp() not null,
    created_at  datetime default current_timestamp() null,
    updated_at  datetime default current_timestamp() null on update current_timestamp()
);

create index idx_punishments_ex_at
    on {prefix}punishments (expires_at);

create index idx_punishments_st
    on {prefix}punishments (target);

create procedure ExpireBans()
BEGIN

    UPDATE {prefix}punishments
    SET length = IF(length - 1 = 0, -1, length - 1)
    WHERE expires_at > NOW()
      AND expires_at <= NOW() + INTERVAL 1 MINUTE;

END;

create definer = root@`%` event expire_basic_admin_punishs_event on schedule
    every '1' MINUTE
        starts '2023-12-14 22:00:06'
    enable
    do
    CALL ExpireBans();

