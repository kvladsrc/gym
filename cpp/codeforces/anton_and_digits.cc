#include <algorithm>
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

int main(int argc, char* argv[]) {
  int k2, k3, k5, k6;
  std::cin >> k2 >> k3 >> k5 >> k6;

  int a = min(min(k5, k6), k2) * 256;
  int b = min(k2 - a / 256, k3) * 32;
  std::cout << a + b << "\n";

  return 0;
}
