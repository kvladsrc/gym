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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  string s;
  std::cin >> s;

  string res;
  int pos = 1;
  int num = 1;
  while (pos <= n) {
    res += s[pos - 1];
    num++;
    pos += num;
  }

  std::cout << res << "\n";

  return 0;
}
