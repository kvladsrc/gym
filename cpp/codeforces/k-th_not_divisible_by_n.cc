#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

// Returns k-th element not divisible by n. Example: n = 3, k = 7,
// then result is 10.
int k_not_divisible(int n, int k) {
  int q = k / (n - 1);
  int r = k % (n - 1);
  if (r == 0) {
    return q * n - 1;
  } else {
    return q * n + r;
  }
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;
    std::cout << k_not_divisible(n, k) << "\n";
  }
  return 0;
}
