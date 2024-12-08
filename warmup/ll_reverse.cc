#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <type_traits>
#include <unordered_map>
#include <vector>

using std::abs;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct node {
  int val;
  node *next;
  node *prev;

  node() : next(nullptr), prev(nullptr) {}
  node(int aval) : node() { val = aval; }

  void reverse() {
    auto b_next = next;
    next = prev;
    prev = b_next;

    if (prev) {
      prev->reverse();
    }
  }

  void print() {
    std::cout << val;
    if (next) {
      std::cout << " ";
      return next->print();
    }
  }
};

struct ll {
  node *head;
  node *tail;
  int size;

  ll() : head(nullptr), tail(nullptr), size(0) {}

  ~ll() {
    while (head) {
      auto buf = head->next;
      delete head;
      head = buf;
    }
  }

  void append(int aval) {
    auto n = new node(aval);
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
      auto buf = tail->prev;
      buf->next = nullptr;
      delete tail;
      tail = buf;
      size--;
      return res;
    }

    return -1;
  }

  void print() {
    if (head) {
      head->print();
      std::cout << "\n";
    }
  }

  void reverse() {
    if (head) {
      head->reverse();
      auto buf = head;
      head = tail;
      tail = buf;
    }
  }
};

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;

  ll l;

  while (n--) {
    int buf;
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
