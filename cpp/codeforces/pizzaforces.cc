#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

const int small_time = 15;
const int medium_time = 20;
const int large_time = 25;
const int small_cnt = 6;
const int medium_cnt = 8;
const int large_cnt = 10;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n;
    std::cin >> n;

    if (n <= small_cnt) {
      std::cout << small_time << "\n";
      continue;
    }
    if (n <= medium_cnt) {
      std::cout << medium_time << "\n";
      continue;
    }
    if (n <= large_cnt) {
      std::cout << large_time << "\n";
      continue;
    }

    if (n % 2) n++;
    int64_t res = (n * 5) / 2;
    std::cout << res << "\n";
  }

  return 0;
}
