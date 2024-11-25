#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, x;
    std::cin >> n >> x;
    int64_t sum = 0;
    int non_divisible_pos = n + 1;  // inf

    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      sum += buf;
      if (buf % x) {
        non_divisible_pos = min(non_divisible_pos, i);
        non_divisible_pos = min(non_divisible_pos, (n - 1) - i);
      }
    }

    if (sum % x) {
      std::cout << n << "\n";
    } else if (non_divisible_pos == n + 1) {
      std::cout << -1 << "\n";
    } else {
      std::cout << n - (non_divisible_pos + 1) << "\n";
    }
  }

  return 0;
}
