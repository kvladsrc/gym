#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static size_t parent(size_t idx) { return idx / 2; }

static size_t l_child(size_t idx) { return idx * 2; }

static size_t r_child(size_t idx) { return (idx * 2) + 1; }

static void swap(int &a, int &b) noexcept {
  int const buf = a;
  a = b;
  b = buf;
}

static void incert(vector<int> &heap, int el) {
  heap.push_back(el);
  size_t idx = heap.size();

  while (idx > 1 && heap[idx - 1] < heap[parent(idx) - 1]) {
    swap(heap[idx - 1], heap[parent(idx) - 1]);
    idx = parent(idx);
  }
}

static int pop(vector<int> &heap) {
  if (heap.empty()) {
    return 0;
  }

  swap(heap.front(), heap.back());
  int const res = heap.back();
  heap.pop_back();

  size_t idx = 1;
  while (true) {
    auto l = l_child(idx);
    auto r = r_child(idx);

    // Cases: l_idx and r_idx available, only l_idx is available, both
    // unavailable.
    size_t min_child = 0;
    if (l < heap.size() && r < heap.size()) {
      min_child = (heap[l - 1] < heap[r - 1] ? l : r);
    } else if (l < heap.size()) {
      min_child = l;
    } else {
      break;
    }

    if (heap[idx - 1] > heap[min_child - 1]) {
      swap(heap[idx - 1], heap[min_child - 1]);
      idx = min_child;
    } else {
      break;
    }
  }

  return res;
}

// Minimal heap.
int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  vector<int> heap;
  while ((n--) != 0) {
    int buf = 0;
    std::cin >> buf;
    incert(heap, buf);
  }

  while (!heap.empty()) {
    auto buf = pop(heap);
    std::cout << buf << "\n";
  }

  return 0;
}
