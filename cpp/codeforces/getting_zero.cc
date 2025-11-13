#include <algorithm>
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
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

const int MODULO = 32768;

int ops_cnt(int n) {
  // Maximal number of oparations is log(32768) = 15. Because (n *
  // 2^15) % 32768 = n * 32768 % 32768 = 0. Sometimes type 1 operation
  // is needed. Lemma: number of type 1 operations should be used
  // before type 2 operations. Proof:
  //
  // Lets represend n in binary form. n = 123 = 1111011. Applying type
  // 2 operation is l-shift: 1111011 -> 11110110. Type 1 operation if
  // preferable if it caueses overflows. Lets brute forece the edge
  // cases.

  int res = 16;
  for (int i = 0; i < 15; ++i) {
    int local = 0;
    local += i;
    auto n_cpy = (n + i) % MODULO;
    while (n_cpy) {
      local++;
      n_cpy = (n_cpy * 2) % MODULO;
    }
    res = min(res, local);
  }
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  for (auto i : a) {
    std::cout << ops_cnt(i) << " ";
  }
  std::cout << "\n";

  return 0;
}
