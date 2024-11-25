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

int sum_of_digits(int a) {
  int res = 0;
  while (a) {
    res += a % 10;
    a /= 10;
  }
  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;

  unordered_map<int, int64_t> table;

  int64_t res = 0;
  for (int i = 1; i <= 200000; ++i) {
    res += sum_of_digits(i);
    table[i] = res;
  }

  while (t--) {
    int n;
    std::cin >> n;
    std::cout << table[n] << "\n";
  }

  return 0;
}
