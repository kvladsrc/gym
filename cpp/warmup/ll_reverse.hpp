#ifndef CPP_WARMUP_LL_REVERSE_H_
#define CPP_WARMUP_LL_REVERSE_H_

namespace warmup {

struct node {
  int val{};
  node *next;
  node *prev;

  node();
  explicit node(int aval);

  void reverse();
  void print() const;
};

struct ll {
  node *head{nullptr};
  node *tail{nullptr};
  int size{0};

  ll() = default;
  ~ll();

  void append(int aval);
  int pop();
  void print() const;
  void reverse();
};

}  // namespace warmup

#endif  // CPP_WARMUP_LL_REVERSE_H_
