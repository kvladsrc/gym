#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  unordered_map<string, string> dict;
  while (m--) {
    string a, b;
    std::cin >> a >> b;
    dict[a] = b;
    dict[b] = a;
  }

  while (n--) {
    string s;
    std::cin >> s;
    if (dict[s].size() < s.size()) {
      std::cout << dict[s] << " ";
    } else {
      std::cout << s << " ";
    }
  }

  return 0;
}
