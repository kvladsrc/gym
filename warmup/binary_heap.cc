#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

namespace {

/*

  From the heap's point of view indexes starts from 1, otherwise
  second node should have 0 * 2 = 0 index.

*/

class BinaryHeap {
  vector<int> heap;

 public:
  BinaryHeap() = default;
  void push(int /*el*/);
  void pop();
  int top() const { return empty() ? -1 : heap.front(); }
  bool empty() const { return heap.empty(); }

 private:
  static size_t parent(size_t idx) { return idx / 2; }
  static size_t l_child(size_t idx) { return idx * 2; }
  static size_t r_child(size_t idx) { return (idx * 2) + 1; }
};

void BinaryHeap::push(int el) {
  heap.push_back(el);
  size_t idx = heap.size();

  while (idx > 1 && heap[idx - 1] < heap[parent(idx) - 1]) {
    std::swap(heap[idx - 1], heap[parent(idx) - 1]);
    idx = parent(idx);
  }
}

void BinaryHeap::pop() {
  if (heap.empty()) {
    return;
  }

  std::swap(heap.front(), heap.back());
  heap.pop_back();

  size_t idx = 1;
  while (true) {
    auto l = l_child(idx);
    auto r = r_child(idx);

    // Cases: l_idx and r_idx available, only l_idx is available, both
    // unavailable.
    size_t min_child = 0;
    if (l <= heap.size() && r <= heap.size()) {
      min_child = (heap[l - 1] < heap[r - 1] ? l : r);
    } else if (l <= heap.size()) {
      min_child = l;
    } else {
      break;
    }

    if (heap[idx - 1] > heap[min_child - 1]) {
      std::swap(heap[idx - 1], heap[min_child - 1]);
      idx = min_child;
    } else {
      break;
    }
  }
}

}  // namespace

// Minimal heap.
int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  BinaryHeap heap;

  while ((n--) != 0) {
    int buf = 0;
    std::cin >> buf;
    heap.push(buf);
  }

  while (!heap.empty()) {
    auto buf = heap.top();
    heap.pop();
    std::cout << buf << "\n";
  }

  return 0;
}
