#include <algorithm>
#include <climits>
#include <cmath>
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
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::cin >> s;

  int remove = 0;
  int stack = 0;
  for (auto c : s) {
    if (c == ')') {
      if (!stack) {
        remove++;
      } else {
        stack--;
      }
    } else {
      stack++;
    }
  }
  remove += stack;

  std::cout << s.size() - remove << "\n";

  return 0;
}
