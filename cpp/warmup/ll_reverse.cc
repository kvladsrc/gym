#include <iostream>
#include <string>

using std::string;

struct node {
  int val{};
  node *next;
  node *prev;

  node() : next(nullptr), prev(nullptr) {}
  explicit node(int aval) : node() { val = aval; }

  void reverse() {
    auto *b_next = next;
    next = prev;
    prev = b_next;

    if (prev != nullptr) {
      prev->reverse();
    }
  }

  void print() const {
    std::cout << val;
    if (next != nullptr) {
      std::cout << " ";
      next->print();
      return;
    }
  }
};

struct ll {
  node *head{nullptr};
  node *tail{nullptr};
  int size{0};

  ll() = default;

  ~ll() {
    while (head != nullptr) {
      auto *buf = head->next;
      delete head;
      head = buf;
    }
  }

  void append(int aval) {
    auto *n = new node(aval);
    if (size > 0) {
      tail->next = n;
      n->prev = tail;
      tail = n;
    } else {
      head = n;
      tail = n;
    }
    size++;
  }

  int pop() {
    if (size == 1) {
      auto res = head->val;
      delete head;
      head = tail = nullptr;
      size = 0;
      return res;
    }

    if (size > 0) {
      auto res = tail->val;
      auto *buf = tail->prev;
      buf->next = nullptr;
      delete tail;
      tail = buf;
      size--;
      return res;
    }

    return -1;
  }

  void print() const {
    if (head != nullptr) {
      head->print();
      std::cout << "\n";
    }
  }

  void reverse() {
    if (head != nullptr) {
      head->reverse();
      auto *buf = head;
      head = tail;
      tail = buf;
    }
  }
};

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  ll l;

  while ((n--) != 0) {
    int buf = 0;
    std::cin >> buf;
    l.append(buf);
  }

  l.reverse();
  l.print();
  auto el = l.pop();
  std::cout << el << "\n";
  l.print();

  return 0;
}
