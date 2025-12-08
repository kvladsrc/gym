#include "cpp/warmup/ll_reverse.hpp"

#include <iostream>

namespace warmup {

node::node() : next(nullptr), prev(nullptr) {}
node::node(int aval) : node() { val = aval; }

void node::reverse() {
  auto *b_next = next;
  next = prev;
  prev = b_next;

  if (prev != nullptr) {
    prev->reverse();
  }
}

void node::print() const {
  std::cout << val;
  if (next != nullptr) {
    std::cout << " ";
    next->print();
    return;
  }
}

ll::~ll() {
  while (head != nullptr) {
    auto *buf = head->next;
    delete head;
    head = buf;
  }
}

void ll::append(int aval) {
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

int ll::pop() {
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

void ll::print() const {
  if (head != nullptr) {
    head->print();
    std::cout << "\n";
  }
}

void ll::reverse() {
  if (head != nullptr) {
    head->reverse();
    auto *buf = head;
    head = tail;
    tail = buf;
  }
}

}  // namespace warmup
