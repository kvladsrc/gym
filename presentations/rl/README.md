# Reinforcement learning: от backpropagation к агентам

Материал для человека, который уже разобрал
[`neural_network/nn_lib`](../../neural_network/nn_lib/README.md).
Начни с [презентации](../build/rl.pdf), затем используй этот конспект
как спецификацию учебного проекта. [Исходник слайдов](rl.tex).
Основной стек проекта: **PyTorch + Gymnasium, затем готовый PPO из
Stable-Baselines3 и среда в Godot**. Использовать или расширять `nn_lib`
не обязательно: она служит мостом к уже знакомой математике.
Это учебный материал и план реализации; самого RL-тренера здесь пока нет.

Сборка из корня репозитория:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c just presentations rl
```

## 1. Как уточнить исходную идею

| Исходная идея | Уточнение |
| --- | --- |
| Общая сеть | Одна policy для независимых копий среды |
| Вероятности действий | Logits → softmax → случайная выборка |
| Градиенты действий | Градиенты log probability выбранных действий |
| Знак обратной связи | Численный вес: return, затем advantage |

Critic можно добавить отдельной сетью или второй головой. Проще хранить
траектории и пересчитать forward/backward после получения наград.
Важны величина, задержка и ожидаемый результат, а не только знак
последней награды.

Это направление называется **policy gradient**; REINFORCE — подходящая
первая реализация. Backpropagation остаётся прежним: меняется способ
получить производную loss по выходу сети.

## 2. Что оптимизируется

На шаге `t` агент получает observation `o_t`, выбирает `a_t`, среда возвращает
`r_t` и `o_(t+1)`. Здесь `r_t` — награда **после** действия `a_t`.
State `s_t` — полное состояние мира; observation — доступная агенту часть.
В формулах ниже считаем наблюдение достаточным и пишем `s_t`.

Markov property: распределение следующего состояния и награды определяется
текущими состоянием и действием, без всей истории. Для движущегося тела
одной позиции недостаточно: нужна скорость. При частичной наблюдаемости
помогают история наблюдений или recurrent policy; это отдельное усложнение.

Эпизод содержит `T` действий, от `0` до `T-1`. Return:

```text
G_t = r_t + gamma*r_(t+1) + ... + gamma^(T-1-t)*r_(T-1)
J(theta) = E[sum_t gamma^t * r_t]
```

`gamma` задаёт предпочтение близких наград, а не learning rate.
В первом проекте эпизоды конечны, поэтому удобно взять `gamma = 1`.
Для явно указанного discounted objective точный episodic policy gradient:

```text
grad J = E[sum_t gamma^t * G_t * grad log pi_theta(a_t | s_t)]
```

Внешний `gamma^t` нельзя молча потерять при этом определении `J`.
В практических actor–critic алгоритмах часто используют другую схему
усреднения состояний и пишут loss без этого множителя. В нашем первом
алгоритме при `gamma = 1` такой неоднозначности нет.

## 3. Почему не нужно дифференцировать игровой движок

Вероятность траектории при динамике среды, не зависящей от параметров policy:

```text
p_theta(tau) = p(s_0) * product_t pi_theta(a_t | s_t) * P(s_(t+1) | s_t, a_t)
grad p_theta(tau) = p_theta(tau) * grad log p_theta(tau)
grad log p_theta(tau) = sum_t grad log pi_theta(a_t | s_t)
```

Подставь это в производную ожидаемого результата. Неизвестная динамика
исчезает из производной логарифма. Дискретное действие не дифференцируем:
дифференцируем его вероятность. Полученная оценка шумная, но позволяет
учиться по сэмплам из обычного симулятора.

Награды до действия не зависят от него причинно и дают нулевой вклад
в среднем. Поэтому вместо return всей траектории используем reward-to-go
`G_t`. Базовый вывод разобран в
[Spinning Up: policy gradient](https://spinningup.openai.com/en/latest/spinningup/rl_intro3.html).

## 4. Loss, знак и численный пример

Для `B` полных эпизодов, `gamma = 1`, минимизируем surrogate loss:

```text
L_actor = -(1/B) * sum_episodes sum_t stop_gradient(w_t) * log pi_theta(a_t | s_t)
w_t = G_t                       # REINFORCE
w_t = G_t - V_phi(s_t)          # с baseline
```

`stop_gradient` означает: вес считается числом при backprop actor.
В ручной библиотеке достаточно передать готовый `double`.
Делим на число эпизодов, а не на их индивидуальные длины: иначе меняется
вклад длинных и коротких эпизодов. Деление всего batch на число переходов
даёт общий масштаб, но для случайных длин это другая выборочная нормировка.
В первом алгоритме используем ровно `1/B`.

Для softmax с logits `z`:

```text
dL/dz_j = w_t * (p_j - one_hot(a_t)_j) / B
```

Пусть `p = [0.2, 0.5, 0.3]`, выбрано второе действие, `B = 1`.
При `w = +2` производная `[0.4, -1.0, 0.6]`:
шаг gradient descent увеличит второй logit.
При `w = -2` направление противоположно. Это пример прямого обновления
logits; общие параметры сети связывают изменения разных состояний.

Для наград `[0, 0, 1]` и `gamma = 0.9` returns равны `[0.81, 0.9, 1]`.
Последнее действие не получает всю заслугу. Но один успешный эпизод
подкрепляет и случайно бесполезные действия: нужны повторения и baseline.

Loss может стать отрицательным. Его падение на старых данных не означает
рост качества policy: оценивай новые эпизоды.

## 5. Реализация на PyTorch и связь с nn_lib

### Основной путь: готовый autograd и поддержка GPU

Для понимания достаточно написать короткий REINFORCE на PyTorch.
Сеть выдаёт **logits**, а `Categorical(logits=...)` выполняет нормализацию,
выборку и вычисление log probability. Пример одного update после сбора
полных эпизодов при `gamma = 1`:

```python
import torch
from torch import nn
from torch.distributions import Categorical

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
policy = nn.Sequential(
    nn.Linear(5, 32), nn.ReLU(),
    nn.Linear(32, 32), nn.ReLU(),
    nn.Linear(32, 4),  # logits, no explicit softmax
).to(device)
optimizer = torch.optim.Adam(policy.parameters(), lr=3e-4)

# Collection: freeze parameters; no optimizer.step() until batch is complete.
# obs_batch, actions_batch, returns_batch come from B complete episodes.
def update(obs_batch, actions_batch, returns_batch, episode_count):
    obs = torch.as_tensor(obs_batch, dtype=torch.float32, device=device)
    actions = torch.as_tensor(actions_batch, dtype=torch.long, device=device)
    weights = torch.as_tensor(returns_batch, dtype=torch.float32, device=device)
    distribution = Categorical(logits=policy(obs))
    loss = -(distribution.log_prob(actions) * weights.detach()).sum() / episode_count
    optimizer.zero_grad()
    loss.backward()
    optimizer.step()
    return loss.item()
```

При сборе действий используй `torch.no_grad()` и
`Categorical(logits=policy(obs)).sample()`, а не `argmax`.
`policy.eval()` сам по себе не отключает случайную выборку.
Этот фрагмент показывает update, а не готовый тренер: сбор эпизодов,
returns, evaluation и сохранение состояния нужно реализовать отдельно.
API распределения: [PyTorch Categorical](https://docs.pytorch.org/docs/stable/distributions.html#categorical).

### Необязательное упражнение: тот же update вручную

Этот подраздел нужен только для сопоставления с освоенным backprop.
Реализовывать его для проекта на PyTorch не требуется.

В [`nn_lib.cc`](../../neural_network/nn_lib/nn_lib.cc):

- `back_propagation` уже начинает с `ans - example`, то есть `p - y`.
  Для actor нужна множительная поправка `weight` ко всему вектору.
- Нельзя подать `weight * one_hot` в существующий `train`:
  получится `p - weight*y`, а нужно `weight*(p-y)`.
- `train` сейчас делит на число строк и сразу вызывает `apply_grads`.
  Для первого RL batch нужно накопить все взвешенные вклады, разделить
  на число эпизодов и применить **одно** обновление.
- `predict` и `forward_pass` изменяют внутренние буферы объекта.
  Один экземпляр `NN` не предназначен для конкурентных вызовов.
  Сначала собирай шаги сред последовательно; позднее нужны отдельные
  inference-копии или независимые рабочие буферы.
- Установи `dropout_keep_probability = 1.0`: сейчас `predict` отключает
  dropout, а `train` включает. Для простого on-policy пересчёта должна
  использоваться та же policy. Начни также с `regularization = 0`.

Возможный **новый**, ещё не реализованный API:

```cpp
// inputs/actions/weights contain all transitions of complete episodes.
// Accumulate weight[i] * (p - one_hot(action[i])) / episode_count.
// Apply gradients once, after the entire batch.
void train_policy(const M& inputs,
                  const std::vector<std::size_t>& actions,
                  const V& weights,
                  std::size_t episode_count);
```

Более общий следующий рефакторинг — backward от `dL/dlogits`.
Для critic потребуется линейный скалярный выход: softmax из одного числа
всегда равен 1 и не годится для предсказания произвольного return.
Cross-entropy actor считай через `log_softmax` / `logsumexp`, чтобы
не получать `log(0)` при округлении вероятностей.

### Псевдокод REINFORCE

```text
initialize policy theta; dropout disabled
repeat:
    freeze theta
    trajectories = collect B complete episodes with sampled actions
    # Environment has a true finite horizon; no external truncation here.
    batch = []
    for trajectory in trajectories:
        G = 0
        for transition in reverse(trajectory):
            G = transition.reward + gamma * G
            weight = gamma**transition.t * G
            batch.append(observation, action, weight)
    zero_grad()
    for observation, action, weight in batch:
        p = forward(observation)  # theta is still the collection policy
        backward_from_logits(weight * (p - one_hot(action)) / B)
    apply_gradients_once()
    discard trajectories
```

Сначала `gamma = 1`, без baseline и нормировки весов.
Хранение данных дешевле хранения градиента по каждому параметру на каждом
шаге. Нельзя сначала сложить все градиенты, а потом умножить на разные
`G_t`: соответствие между шагами и весами уже потеряно.

Полные эпизоды должны собираться по заданным reset seeds, без отбрасывания
долгих неудачных попыток ради первых быстро закончившихся эпизодов.

## 6. Baseline, critic и advantage

`V^pi(s) = E[G_t | s_t=s]` оценивает результат текущей policy.
`Q^pi(s,a)` — результат после конкретного действия и дальнейшего следования
policy; `A^pi(s,a) = Q^pi(s,a) - V^pi(s)`.

Если ожидается 5, а получилось 2, advantage равен `-3`, хотя reward
положителен. Именно это уточняет идею «применить с соответствующим знаком».
Вычитание baseline, зависящего от состояния, но не выбранного действия,
сохраняет ожидаемый policy gradient:

```text
E_a[b(s) * grad log pi(a|s)] = b(s) * grad sum_a pi(a|s) = 0
```

Это свойство фиксированного baseline; оценка baseline по тому же маленькому
batch может вносить смещение. Сначала можно использовать средний return
**предыдущих** batches. Для critic вычисляй actor weights старым critic,
до его обучения на текущих returns. Обучение critic — регрессия:

```text
L_value = mean((V_phi(s_t) - stop_gradient(G_t))^2) / 2
```

Monte Carlo ждёт конца эпизода. Temporal difference (TD) позволяет
подставить оценку продолжения — bootstrapping:

```text
y_t = r_t + gamma * (1 - terminated_t) * V_old(next_observation)
delta_t = y_t - V_old(observation)
```

`delta_t` служит одношаговой оценкой advantage. Неточный critic добавляет
смещение; зато ждать полного исхода не нужно. Targets фиксируются на время
обновления. Начни с двух отдельных сетей, чтобы actor loss случайно
не обучал critic через weight.

Generalized Advantage Estimation (GAE) суммирует TD residuals:

```text
A_t = delta_t + gamma * lambda * continuation_t * A_(t+1)
```

`continuation` равен 0 на границе эпизода и batch; TD bootstrap при этом
может оставаться ненулевым. `lambda` управляет компромиссом между смещением
и дисперсией. На границе rollout вычисли value последнего состояния.
Подробнее: [статья GAE](https://arxiv.org/abs/1506.02438).

### Termination и truncation

`terminated` означает конец задачи: bootstrap равен 0.
`truncated` означает внешнее прерывание сбора: продолжение оценивается
critic по последнему наблюдению **до reset**. При обоих флагах конец задачи
имеет приоритет. GAE не переносится через reset.

Если лимит времени — часть правил задачи, это termination, а оставшееся
время должно входить в observation. Для REINFORCE без critic собирай
полные эпизоды: произвольный обрыв не даёт полного Monte Carlo return.
Семантика соответствует
[Gymnasium: Handling Time Limits](https://gymnasium.farama.org/main/tutorials/handling_time_limits/).

## 7. Зачем PPO и где остальные алгоритмы

| Семейство | Что учим | Особенность |
| --- | --- | --- |
| REINFORCE | Policy | Полные returns, высокая дисперсия |
| Actor–critic | Policy и value | Baseline, TD и bootstrap |
| PPO | Policy и value | Несколько epochs на свежем batch |
| Q-learning / DQN | Q(s,a) | Bellman targets и exploration |
| SAC | Actor и Q critics | Off-policy, entropy objective |
| Model-based RL | Модель динамики | Планирование последствий |

DQN также использует replay buffer и target network.
SAC и model-based методы оставляем за пределами первого проекта.

On-policy данные собраны текущей policy. После обновления распределение
действий меняется: многократный обычный `train` на старом batch уже
не оценивает исходный on-policy gradient. PPO использует отношение
вероятностей и ограниченный surrogate objective:

```text
rho_t = exp(log_pi_new(a_t|s_t) - log_pi_old(a_t|s_t))
L_clip = -mean(min(rho_t*A_t, clip(rho_t, 1-eps, 1+eps)*A_t))
```

Старые log probabilities и advantages фиксированы. Можно делать несколько
minibatch epochs, затем собирать новый batch. Clipping не гарантирует
жёсткую границу изменения policy; проверяй KL divergence и entropy.
Отрицательный entropy term в минимизируемом actor loss поощряет
исследование. PPO не устраняет ошибки reward и observations.
Первоисточник: [Schulman et al., PPO](https://arxiv.org/abs/1707.06347).

Для непрерывных действий softmax заменяется, например, Gaussian policy:
сеть предсказывает параметры распределения. Ограничение действий через
`tanh` требует поправки к log probability. Это следующий проект;
дискретные действия позволяют сначала проверить всю цепочку.

## 8. Учебный проект: добраться до цели

Ниже — предлагаемый дизайн эксперимента, а не требования алгоритма RL.

Первая среда — пустая сетка 7×7 с агентом и целью, без инерции и препятствий.
Reset выбирает разные клетки равномерно. Действия: вверх, вниз, влево,
вправо. Попытка выйти за край оставляет агента на месте и тратит ход.
Максимум 40 действий — часть правил игры, затем эпизод заканчивается.
Если цель достигнута на последнем ходу, это успех.

Observation: `[x/6, y/6, goal_x/6, goal_y/6, remaining_steps/40]`.
Координаты абсолютные: одно смещение до цели не сообщает расстояние до стен.
Награда: `+1` за достижение цели, `-0.01` за любой другой ход.
Награды не суммируются на успешном шаге. `gamma = 1`.
Policy: MLP `5 → 32 → 32 → 4`, ReLU и softmax.
Начальные экспериментальные настройки: batch 64 полных эпизода,
Adam learning rate `3e-4`, затем `1e-4` и `1e-3`, если обучение не идёт.
Это стартовые гипотезы, а не обещание сходимости.

Ручная policy, каждый раз сокращающая Manhattan distance, достигает цели
за максимум 12 действий. Это oracle для проверки среды.
Случайная policy задаёт измеряемый baseline; сначала действительно измерь
его на тех же evaluation seeds.

После этого перенеси задачу в 2D игровой движок: добавь непрерывные
координаты и скорость, оставь дискретные команды управления. Физика
делает новую задачу отличной от сетки: переносу весов нельзя доверять
без проверки. В observation добавятся скорости и информация о границах.
Препятствия, лучевые сенсоры, изображения и взаимодействующие агенты —
последующие этапы, каждый по отдельности.

### Контракт среды и тренера

```text
reset(seed) -> observation, info
step(action) -> next_observation, reward, terminated, truncated, info
```

Тренер владеет параметрами, optimizer и batch. Среда владеет состоянием,
физикой, наградами и reset. Один action соответствует фиксированному числу
physics ticks; рендеринг не меняет ход симуляции. В headless режиме считаем
шаги максимально быстро, сохраняя тот же simulation timestep.

Для первого прототипа годится один Python-процесс с Gymnasium-средой.
Для Godot сначала оцени готовый адаптер Godot RL Agents (см. ниже).
Если потребуется свой мост между Python trainer и игровым движком,
передавай batch команд/наблюдений через локальный IPC
с `env_id`, `episode_id`, `step_id` и `policy_version`.
Сначала корректность и воспроизводимость, затем оптимизация IPC.
Пауза на обновление весов даёт простой синхронный on-policy сбор.

### Одна policy и много агентов

16 независимых копий среды с общей policy — способ собирать опыт.
У каждой своя траектория; returns никогда не пересекают границу среды
или эпизода. Параметры заморожены на всём этапе сбора.

16 персонажей в одной арене — уже multi-agent задача: действия соседей
меняют условия, возникает вопрос индивидуальной/общей награды и credit
assignment. Parameter sharing возможно для одинаковых ролей, но не решает
эти вопросы автоматически. Начни с независимых арен.

## 9. Этапы и критерии готовности

1. **Bandit, один шаг.** Три действия с вероятностями награды
   `[0.1, 0.5, 0.9]`, постоянное наблюдение. Проверь weighted backward
   конечными разностями при положительном и отрицательном weight,
   с отключёнными dropout и L2. При нулевом weight параметры не меняются.
   Цель эксперимента: вероятность третьего действия выше 0.9
   в большинстве из пяти запусков в пределах 100 000 взаимодействий.
2. **Сетка и REINFORCE.** Проверь reset, стены, награды, success на последнем
   ходу и returns вручную. Обучи policy без critic. Лимит первого запуска —
   1 миллион шагов, после него разбирай метрики, а не запускай бесконечно.
3. **Baseline и critic.** На одинаковом бюджете шагов сравни REINFORCE,
   baseline предыдущих batches и learned value. Проверь value targets
   на терминальном переходе. Улучшение скорости обучения — эксперимент,
   а не обязательный результат каждого seed.
4. **Actor–critic и GAE.** Добавь короткие rollouts, обработай внешнее
   прерывание и bootstrap по final observation. Вручную проверь цепочку
   из двух переходов, в том числе reset между ними.
5. **Игровой движок.** Сначала проверь random и ручную policy, затем обучение.
   Сравни фиксированную последовательность действий с рендером и без него.
6. **Готовый PPO, затем параллелизм.** Подключи Stable-Baselines3;
   собственная реализация PPO остаётся необязательным упражнением.
   Проверь `rho = 1` до первого обновления,
   оба знака advantage и ветки clipping. Сравни с предыдущим алгоритмом
   при одинаковом числе взаимодействий, а не числе updates.

Предлагаемый критерий готовности сетки: не менее 90% успехов на 500
фиксированных evaluation seeds минимум у 4 из 5 независимо обученных
моделей. Evaluation seeds не использовать для обучения; если по ним
выбирались настройки, финальный результат измерь ещё на отдельном наборе.
Отдельно оцени sampled policy и greedy (`argmax`), не смешивай их результаты.

Записывай environment steps, success rate, средний return, длину эпизода,
entropy, gradient norm, seed и версию конфигурации. Для critic добавь
value loss, для PPO — KL и clip fraction. Построй кривые по шагам среды
с разбросом между seeds; сохрани checkpoint и воспроизводимый replay.

## 10. Готовый стек и что действительно ускоряет GPU

Рекомендуемый путь реализации:

- **PyTorch** для учебного REINFORCE: autograd, Adam, тензоры на CPU/GPU.
- **Gymnasium** для контракта первой среды и независимой проверки её правил.
- **Stable-Baselines3 PPO** для готового алгоритма, когда переходишь
  к более сложной среде. Перед обучением проверь совместимость среды
  через `stable_baselines3.common.env_checker.check_env`.
- **Godot + Godot RL Agents** для среды в игровом движке. Проект предоставляет
  мост к Python и примеры обучения; проверь его требования к версиям
  Godot и зависимостей перед интеграцией.
- **TorchRL** — альтернативный следующий шаг, если захочется собирать
  алгоритмы из готовых компонентов и контролировать сбор данных подробнее.
  Для первого проекта изучать одновременно SB3 и TorchRL не нужно.

Например, **после реализации Gymnasium-совместимой `env`**:

```python
from stable_baselines3 import PPO
from stable_baselines3.common.env_checker import check_env

check_env(env)
model = PPO("MlpPolicy", env, device="auto", seed=0, verbose=1)
model.learn(total_timesteps=1_000_000)
model.save("reach_goal_ppo")
```

Это пример подключения, а не настроенная конфигурация эксперимента:
для сравнения с REINFORCE явно согласуй `gamma`, архитектуру,
бюджет взаимодействий и evaluation. При использовании готового Godot
адаптера следуй его примеру: vector API может отличаться от обычного
Gymnasium `Env`, и `check_env` применяется именно к обычному `Env`.

GPU ускоряет батчевые операции сети и backprop. `device="cuda"` требует
совместимой установки PyTorch и устройства; при отсутствии такого backend
пример выше с `torch.cuda.is_available()` выбирает CPU. Конкретные команды
установки зависят от оборудования и не являются частью этого материала.

Обычная физика Godot и обмен с Python автоматически на GPU не переезжают.
Для маленькой MLP ожидание среды и передача небольших массивов могут
стоить больше вычислений. Документация SB3 прямо рекомендует рассмотреть
CPU для PPO с MLP. Сравни wall time, environment steps/s и время update
на одинаковом эксперименте; увеличивай число независимых сред и размер
batch по измерениям. Изображения и более крупные сети делают GPU полезнее.

Источники:
[SB3 PPO](https://stable-baselines3.readthedocs.io/en/stable/modules/ppo.html),
[Godot RL Agents](https://github.com/edbeeching/godot_rl_agents),
[TorchRL PPO tutorial](https://docs.pytorch.org/tutorials/intermediate/reinforcement_ppo.html).

## 11. Самопроверка

- Почему `train(observation, chosen_action)` без weight не учит награде?
- Почему нельзя использовать только последнюю награду для всех градиентов?
- Почему положительный return может дать отрицательный advantage?
- Почему softmax с одним выходом не работает как critic?
- Когда timeout означает termination, а когда truncation?
- Почему после обновления policy нельзя бесконечно учить REINFORCE
  на тех же эпизодах?

Ответы: выбранное действие само по себе не является правильной меткой;
разные шаги имеют разные будущие returns; baseline может быть выше return;
softmax одного числа константен; важно, входит ли лимит в задачу;
данные больше не соответствуют текущему распределению policy.

## 12. Источники и маршрут чтения

1. [Sutton & Barto, Reinforcement Learning: An Introduction](http://incompleteideas.net/book/the-book-2nd.html),
   главы 2–3 (bandits, MDP), 6 (TD), 13 (policy gradient).
   Это базовый учебник для терминов и общей карты области.
2. [Spinning Up: Intro to Policy Optimization](https://spinningup.openai.com/en/latest/spinningup/rl_intro3.html)
   — разобрать вывод log-derivative trick после разделов 2–4.
3. [Gymnasium: Handling Time Limits](https://gymnasium.farama.org/main/tutorials/handling_time_limits/)
   — прочитать перед реализацией bootstrap и reset.
4. [Schulman et al., Generalized Advantage Estimation](https://arxiv.org/abs/1506.02438)
   — после рабочей Monte Carlo версии с critic.
5. [Schulman et al., Proximal Policy Optimization Algorithms](https://arxiv.org/abs/1707.06347)
   — после actor–critic; выписать objective и проверить знаки на числах.
