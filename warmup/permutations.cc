#include <algorithm>
#include <chrono>
#include <climits>
#include <cmath>
#include <cstddef>
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
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

void print(vector<int> &a) {
  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";
}

// Heap's algorithm.
int64_t permutations_heaps(std::vector<int> &arr, int size) {
  if (size == 1) {
    return 1;
  }

  int64_t res = 0;
  for (int i = 0; i < size; i++) {
    res += permutations_heaps(arr, size - 1);
    // If size is odd, swap first and last element
    if (size % 2 == 1) {
      std::swap(arr[0], arr[size - 1]);
    }
    // If size is even, swap i-th and last element
    else {
      std::swap(arr[i], arr[size - 1]);
    }
  }

  return res;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  vector<int> perms(n);
  for (int i = 0; i < n; ++i) {
    perms[i] = i;
  }
  auto start_heaps = std::chrono::high_resolution_clock::now();
  auto t = permutations_heaps(perms, n);
  auto end_heaps = std::chrono::high_resolution_clock::now();
  auto duration_heaps =
      std::chrono::duration_cast<std::chrono::seconds>(end_heaps - start_heaps);

  std::cout << "Duration: " << duration_heaps.count() << "\n";
  std::cout << "Permutations: " << t << "\n";

  return 0;
}
