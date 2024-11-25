#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <functional>
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
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

pair<int, int> divide(int a, int b) {
  if (a < b) {
    return {0, a};
  }

  auto buf = divide(a / 2, b);
  buf.first *= 2;
  buf.second *= 2;
  buf.second += a % 2;
  if (buf.second >= b) {
    buf.second -= b;
    buf.first++;
  }
  return buf;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b;
    std::cin >> a >> b;
    auto res = divide(a, b);
    if ((res.first != a / b) || res.second != a % b) {
      std::cout << "Wrong!\n";
    } else {
      std::cout << "Ok!\n";
    }
  }

  return 0;
}
