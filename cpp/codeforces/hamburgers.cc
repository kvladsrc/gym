#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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

int main(int argc, char* argv[]) {
  string receipt;
  std::cin >> receipt;
  int n_b, n_s, n_c;
  std::cin >> n_b >> n_s >> n_c;
  int p_b, p_s, p_c;
  std::cin >> p_b >> p_s >> p_c;
  int64_t money;
  std::cin >> money;

  unordered_map<char, int> bucket;
  for (char c : receipt) {
    bucket[c]++;
  }

  int64_t res = 100;
  if (bucket['B']) res = min(static_cast<int>(res), n_b / bucket['B']);
  if (bucket['S']) res = min(static_cast<int>(res), n_s / bucket['S']);
  if (bucket['C']) res = min(static_cast<int>(res), n_c / bucket['C']);

  // Using 0 for ingredients missing in the receipt because of while
  // condition.
  n_b -= (bucket['B'] ? bucket['B'] * res : n_b);
  n_s -= (bucket['S'] ? bucket['S'] * res : n_s);
  n_c -= (bucket['C'] ? bucket['C'] * res : n_c);

  int full_burger_price =
      bucket['B'] * p_b + bucket['S'] * p_s + bucket['C'] * p_c;

  // We use existing leftovers. Since constraints are just 1<=n<=100
  // that is fine.
  while (n_b || n_s || n_c) {
    int half_price = full_burger_price;
    if (n_b) {
      int can_use = min(bucket['B'], n_b);
      half_price -= can_use * p_b;
      n_b -= can_use;
    }
    if (n_s) {
      int can_use = min(bucket['S'], n_s);
      half_price -= can_use * p_s;
      n_s -= can_use;
    }
    if (n_c) {
      int can_use = min(bucket['C'], n_c);
      half_price -= can_use * p_c;
      n_c -= can_use;
    }

    if (money < half_price) break;

    money -= half_price;
    res++;
  }

  res += money / full_burger_price;
  std::cout << res << "\n";

  return 0;
}
