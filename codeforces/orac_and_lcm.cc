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
using std::endl;
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

int64_t gcd(int64_t a, int64_t b) {
  if (a % b == 0) {
    return abs(b);
  }
  return gcd(b, a % b);
}

int64_t lcm(int64_t a, int64_t b) { return (a * b) / gcd(a, b); }

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n;
  std::cin >> n;
  vector<int64_t> a(n);
  int64_t l = 1;
  for (auto &i : a) {
    std::cin >> i;
    l = lcm(l, i);
  }

  // GCD_j(a_1, a_2, ..., a_i | i != j) = GCD(front_gcd[i - 1],
  // back_gcd[i + 1]); Result is GCD(LCM(a_i, GCD_i | i <= i <=
  // n)). Can be simplified to one loop, calculating result while
  // reading input, but decide to keep the front and back gcd vectors.
  vector<int64_t> front_gcd(n);
  vector<int64_t> back_gcd(n);
  front_gcd.front() = a.front();
  back_gcd.back() = a.back();
  for (size_t idx = 1; idx < a.size(); ++idx) {
    front_gcd[idx] = gcd(front_gcd[idx - 1], a[idx]);
    back_gcd[a.size() - 1 - idx] =
        gcd(back_gcd[a.size() - 1 - (idx - 1)], a[a.size() - 1 - idx]);
  }

  int64_t global = lcm(front_gcd[a.size() - 2], a.back());
  global = max(global, lcm(back_gcd[1], a.front()));
  for (size_t idx = 1; idx < a.size() - 1; ++idx) {
    int64_t local = lcm(a[idx], gcd(front_gcd[idx - 1], back_gcd[idx + 1]));
    global = gcd(local, global);
  }

  std::cout << global << "\n";

  return 0;
}
