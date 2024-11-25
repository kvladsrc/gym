#include <algorithm>
#include <climits>
#include <cmath>
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
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int multiply(int a, int b) {
  if (a == 1) {
    return b;
  }

  if (a % 2 == 0) {
    return multiply(a / 2, b * 2);
  }

  return b + multiply(a / 2, b * 2);
}

int main(int argc, char *argv[]) {
  int a, b;
  std::cin >> a >> b;
  std::cout << multiply(a, b) << "\n";
  return 0;
}
