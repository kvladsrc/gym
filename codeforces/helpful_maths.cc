#include <iostream>
#include <string>

using std::string;

// O(n)
void print_non_decreasing(string &a) {
  int one = 0, two = 0, three = 0;
  for (char c : a) {
    switch (c) {
      case '1':
        one++;
        break;
      case '2':
        two++;
        break;
      case '3':
        three++;
        break;
      default:
        continue;
    }
  }

  string res;

  while (one) {
    res += "1+";
    one--;
  }

  while (two) {
    res += "2+";
    two--;
  }

  while (three) {
    res += "3+";
    three--;
  }

  if (!res.empty()) {
    res = res.substr(0, res.size() - 1);
  }

  std::cout << res << "\n";
}

int main(int argc, char *argv[]) {
  string input;
  std::cin >> input;
  print_non_decreasing(input);
  return 0;
}
