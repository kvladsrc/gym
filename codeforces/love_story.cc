#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
  int t;
  std::cin >> t;
  string sample("codeforces");
  while (t--) {
    string in;
    std::cin >> in;
    int res = 0;
    for (size_t i = 0; i < sample.size(); ++i) {
      if (in[i] != sample[i]) res++;
    }
    std::cout << res << "\n";
  }
  return 0;
}
