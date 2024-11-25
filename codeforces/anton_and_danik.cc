#include <iostream>
#include <string>

using std::string;

// -1 is for Anton, 1 is fo Danik, 0 is for friendship.
int winer(string &a) {
  int a_w = 0, d_w = 0;
  for (char c : a) {
    if (c == 'A') {
      a_w++;
    } else {
      d_w++;
    }
  }

  if (a_w > d_w) return -1;
  if (a_w < d_w) return 1;
  return 0;
}

int main(int argc, char *argv[]) {
  string table;
  int skip;
  std::cin >> skip;
  std::cin >> table;
  int result = winer(table);
  switch (result) {
    case 0:
      std::cout << "Friendship"
                << "\n";
      break;
    case 1:
      std::cout << "Danik"
                << "\n";
      break;
    default:
      std::cout << "Anton"
                << "\n";
  }

  return 0;
}
