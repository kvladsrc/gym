#include <algorithm>
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

int mod(int a, int b) {
  int result = a % b;
  if (result < 0) {
    result += b;
  }
  return result;
}

int main(int argc, char *argv[]) {
  int f_1, f_2;
  std::cin >> f_1 >> f_2;
  int n;
  std::cin >> n;

  vector<int64_t> unique = {mod(f_1, 1000000007), mod(f_2, 1000000007)};
  for (int i = 0; i < 6; ++i) {
    int next = mod((f_2 - f_1), 1000000007);
    unique.push_back(next);
    f_1 = f_2;
    f_2 = next;
  }

  n = n % 6;
  if (!n) n = 6;
  std::cout << unique[n - 1] << "\n";

  return 0;
}
